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

    var settings = MigrationSettingsValidator.Validate(parseResult.Options!);

    console.Step("Validating connections");
    console.Info($"Origin:      {settings.Origin.RedactedConnectionString}");
    console.Info($"Destination: {settings.Destination.RedactedConnectionString}");

    await using var origin = new NpgsqlConnection(settings.Origin.ConnectionString);
    await using var destination = new NpgsqlConnection(settings.Destination.ConnectionString);

    await origin.OpenAsync(cancellation.Token);
    await destination.OpenAsync(cancellation.Token);

    var inspector = new PostgresSchemaInspector(origin);
    var tables = await inspector.GetUserTablesAsync(
        settings.Schema,
        settings.TableFilter,
        cancellation.Token);

    var planner = new MigrationPlanner();
    var plan = planner.CreatePlan(settings, tables);

    console.Plan(plan);

    if (settings.DryRun)
    {
        console.Success("Dry run complete. No data was copied.");
        return ExitCodes.Success;
    }

    var copier = new CopyDataMigrator(origin, destination, console);
    var result = await copier.CopyAsync(plan, cancellation.Token);

    console.Success($"Copied {result.TablesCopied} table(s), {result.RowsCopied} row(s).");
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
