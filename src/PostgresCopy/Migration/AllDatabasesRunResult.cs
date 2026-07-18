// File: src/PostgresCopy/Migration/AllDatabasesRunResult.cs

namespace PostgresCopy.Migration;

public sealed record PerDatabaseResult(
    string DatabaseName,
    bool Succeeded,
    MigrationRunResult? Result,
    string? FailureMessage,
    TimeSpan Elapsed);

public sealed record AllDatabasesRunResult(
    int TotalDatabases,
    int Succeeded,
    int Failed,
    IReadOnlyList<PerDatabaseResult> Results);

public sealed record AllDatabasesMigrationSettings(
    string OriginConnectionString,
    string DestinationConnectionString,
    IReadOnlyList<string> ExcludeDatabases,
    bool DryRun,
    bool Verify,
    bool Yes,
    int BatchSize,
    bool Verbose);
