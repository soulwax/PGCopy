using PostgresCopy.Config;
using PostgresCopy.Database;

namespace PostgresCopy.Migration;

public sealed class MigrationPlanner
{
    public MigrationPlan CreatePlan(MigrationSettings settings, IReadOnlyList<TableInfo> tables)
    {
        var plannedTables = tables
            .Select(table => new TableMigrationPlan(table))
            .ToArray();

        return new MigrationPlan(settings.Schema, plannedTables, settings.DryRun);
    }
}
