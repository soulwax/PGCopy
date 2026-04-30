namespace PostgresCopy.Migration;

public sealed record MigrationRunResult(
    bool DryRun,
    int TablesCopied,
    long RowsCopied);
