using PostgresCopy.Config;
using PostgresCopy.Database;

namespace PostgresCopy.Migration;

public sealed class DestinationPreflightValidator
{
    public void Validate(MigrationPlan plan, IReadOnlyList<TableInfo> destinationTables)
    {
        if (plan.Tables.Count == 0)
        {
            throw new ValidationException("No origin tables matched the migration plan.");
        }

        var destinationByName = destinationTables.ToDictionary(
            table => table.Name,
            StringComparer.Ordinal);

        var errors = new List<string>();

        foreach (var plannedTable in plan.Tables)
        {
            if (!destinationByName.TryGetValue(plannedTable.Table.Name, out var destinationTable))
            {
                errors.Add($"{plannedTable.QualifiedName}: destination table is missing.");
                continue;
            }

            if (!ColumnsMatch(plannedTable.Table.Columns, destinationTable.Columns))
            {
                errors.Add($"{plannedTable.QualifiedName}: destination columns do not match origin columns.");
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException("Destination preflight failed:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }
    }

    private static bool ColumnsMatch(IReadOnlyList<string> originColumns, IReadOnlyList<string> destinationColumns)
    {
        return originColumns.SequenceEqual(destinationColumns, StringComparer.Ordinal);
    }
}
