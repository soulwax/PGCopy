namespace PostgresCopy.Migration;

public sealed record MigrationPlan(
    string Schema,
    IReadOnlyList<TableMigrationPlan> Tables,
    bool DryRun,
    bool TruncateDestination,
    bool Verify);
