namespace PostgresCopy.Config;

public sealed record MigrationSettings(
    DatabaseEndpoint Origin,
    DatabaseEndpoint Destination,
    string Schema,
    IReadOnlyList<string> TableFilter,
    bool DryRun,
    bool Verbose,
    bool Yes,
    int BatchSize);
