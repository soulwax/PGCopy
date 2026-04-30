using PostgresCopy.Config;
using PostgresCopy.Database;

namespace PostgresCopy.Migration;

public sealed class MigrationPlanner
{
    public MigrationPlan CreatePlan(
        MigrationSettings settings,
        IReadOnlyList<TableInfo> tables,
        IReadOnlyList<TableDependency>? dependencies = null)
    {
        var orderedTables = OrderByDependencies(tables, dependencies ?? []);
        var plannedTables = orderedTables
            .Select(table => new TableMigrationPlan(table))
            .ToArray();

        return new MigrationPlan(
            settings.Schema,
            plannedTables,
            settings.DryRun,
            settings.TruncateDestination,
            settings.Verify);
    }

    private static IReadOnlyList<TableInfo> OrderByDependencies(
        IReadOnlyList<TableInfo> tables,
        IReadOnlyList<TableDependency> dependencies)
    {
        if (dependencies.Count == 0)
        {
            return tables;
        }

        var originalOrder = tables
            .Select((table, index) => new { table.Name, Index = index })
            .ToDictionary(item => item.Name, item => item.Index, StringComparer.Ordinal);
        var byName = tables.ToDictionary(table => table.Name, StringComparer.Ordinal);
        var remaining = tables.Select(table => table.Name).ToHashSet(StringComparer.Ordinal);
        var ordered = new List<TableInfo>(tables.Count);

        while (remaining.Count > 0)
        {
            var ready = remaining
                .Where(tableName => dependencies
                    .Where(dependency => dependency.TableName.Equals(tableName, StringComparison.Ordinal))
                    .All(dependency => !remaining.Contains(dependency.DependsOnTableName)))
                .OrderBy(tableName => originalOrder[tableName])
                .ToArray();

            if (ready.Length == 0)
            {
                return tables;
            }

            foreach (var tableName in ready)
            {
                ordered.Add(byName[tableName]);
                remaining.Remove(tableName);
            }
        }

        return ordered;
    }
}
