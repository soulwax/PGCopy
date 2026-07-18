// File: src/PostgresCopy/Program.cs

using Npgsql;
using PostgresCopy.Cli;
using PostgresCopy.Config;
using PostgresCopy.Database;
using PostgresCopy.Logging;
using PostgresCopy.Migration;

var console = new ConsoleMigrationLogger();
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
    console.Error("Cancellation requested. Stopping after the current safe point.");
};

try
{
    args = InteractiveCliPrompt.FillMissingRequiredOptions(args);

    var parseResult = CliOptionsParser.Parse(args);
    if (parseResult.ShowHelp)
    {
        console.Info(CliOptionsParser.HelpText);
        return ExitCodes.Success;
    }

    if (!parseResult.Success)
    {
        console.Error(parseResult.ErrorMessage ?? "Invalid command line.");
        console.Info(CliOptionsParser.HelpText);
        return ExitCodes.ValidationFailure;
    }

    var options = parseResult.Options!;

    if (options.AllDatabases)
    {
        var allDatabasesOrigin = PostgresConnectionString.Parse(options.Origin);
        var allDatabasesDestination = PostgresConnectionString.Parse(options.Destination);

        if (allDatabasesOrigin.ComparisonKey.Equals(allDatabasesDestination.ComparisonKey, StringComparison.Ordinal))
        {
            throw new ValidationException("Origin and destination point to the same database. Refusing to continue.");
        }

        var allDatabasesSettings = new AllDatabasesMigrationSettings(
            allDatabasesOrigin.ConnectionString,
            allDatabasesDestination.ConnectionString,
            options.ExcludeDatabases,
            options.DryRun,
            options.Verify,
            options.Yes,
            options.BatchSize,
            options.Verbose);

        bool allDatabasesConfirmed;
        if (options.DryRun)
        {
            allDatabasesConfirmed = true;
        }
        else
        {
            var maintenanceConnectionString = PostgresConnectionString.WithDatabase(
                allDatabasesOrigin.ConnectionString, DestinationDatabaseLifecycle.DefaultMaintenanceDatabase);
            await using var probeConnection = new NpgsqlConnection(maintenanceConnectionString);
            await probeConnection.OpenAsync(cancellation.Token);
            var allDatabaseNames = await DestinationDatabaseLifecycle.ListDatabasesAsync(probeConnection, cancellation.Token);
            var selectedNames = AllDatabasesMigrationRunner.FilterSelectedDatabases(allDatabaseNames, options.ExcludeDatabases);
            allDatabasesConfirmed = DestructiveActionPrompt.ConfirmOverwriteAllDatabases(selectedNames, options.Yes);
        }

        var allDatabasesSummary = await new AllDatabasesMigrationRunner(console).RunAsync(
            allDatabasesSettings,
            allDatabasesConfirmed,
            cancellation.Token);

        console.Info($"Completed {allDatabasesSummary.TotalDatabases} database(s): {allDatabasesSummary.Succeeded} succeeded, {allDatabasesSummary.Failed} failed.");
        return allDatabasesSummary.Failed == 0 ? ExitCodes.Success : ExitCodes.MigrationFailure;
    }

    var settings = MigrationSettingsValidator.Validate(options);

    var truncateOk = !settings.TruncateDestination
        || DestructiveActionPrompt.ConfirmTruncateDestination(settings.Yes);
    var dropOk = !settings.DropSchema
        || DestructiveActionPrompt.ConfirmDropSchema(settings.Schema, settings.Yes);
    var destructiveActionsConfirmed = truncateOk && dropOk;

    await new MigrationRunner(console).RunAsync(
        settings,
        destructiveActionsConfirmed,
        cancellation.Token);

    return ExitCodes.Success;
}
catch (ValidationException ex)
{
    console.Error(ex.Message);
    return ExitCodes.ValidationFailure;
}
catch (MigrationTableException ex)
{
    console.Error(ex.Message);
    console.Error($"Copied before failure: {ex.TablesCopiedBeforeFailure} table(s), {ex.RowsCopiedBeforeFailure} row(s).");
    return ExitCodes.MigrationFailure;
}
catch (VerificationException ex)
{
    console.Error(ex.Message);
    return ExitCodes.MigrationFailure;
}
catch (PostgresException ex)
{
    console.Error($"PostgreSQL error: {ex.MessageText}");
    return ExitCodes.MigrationFailure;
}
catch (OperationCanceledException)
{
    console.Error("Migration cancelled.");
    return ExitCodes.MigrationFailure;
}
catch (Exception ex)
{
    console.Error(ex.Message);

    if (args.Contains("--verbose", StringComparer.OrdinalIgnoreCase))
    {
        console.Error(ex.ToString());
    }

    return ExitCodes.MigrationFailure;
}

internal static class ExitCodes
{
    public const int Success = 0;
    public const int ValidationFailure = 1;
    public const int MigrationFailure = 2;
}
