using Npgsql;
using PostgresCopy.Cli;
using PostgresCopy.Config;
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

    var settings = MigrationSettingsValidator.Validate(parseResult.Options!);

    var destructiveActionsConfirmed = !settings.TruncateDestination
        || DestructiveActionPrompt.ConfirmTruncateDestination(settings.Yes);

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
