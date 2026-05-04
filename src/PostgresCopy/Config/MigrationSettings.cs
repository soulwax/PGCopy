// File: src/PostgresCopy/Config/MigrationSettings.cs

namespace PostgresCopy.Config;

public sealed record MigrationSettings(
    DatabaseEndpoint Origin,
    DatabaseEndpoint Destination,
    string Schema,
    IReadOnlyList<string> TableFilter,
    bool DryRun,
    bool TruncateDestination,
    bool Verify,
    bool Verbose,
    bool Yes,
    int BatchSize,
    bool CreateSchema);
