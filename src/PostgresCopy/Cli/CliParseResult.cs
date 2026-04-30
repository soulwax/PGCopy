namespace PostgresCopy.Cli;

public sealed record CliParseResult(
    bool Success,
    CliOptions? Options,
    bool ShowHelp,
    string? ErrorMessage)
{
    public static CliParseResult Help() => new(true, null, true, null);

    public static CliParseResult Failed(string message) => new(false, null, false, message);

    public static CliParseResult Parsed(CliOptions options) => new(true, options, false, null);
}
