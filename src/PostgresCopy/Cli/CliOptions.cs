namespace PostgresCopy.Cli;

public sealed record CliOptions(
    string Origin,
    string Destination,
    string Schema,
    IReadOnlyList<string> Tables,
    bool DryRun,
    bool TruncateDestination,
    bool Verify,
    bool Verbose,
    bool Yes,
    int BatchSize);
