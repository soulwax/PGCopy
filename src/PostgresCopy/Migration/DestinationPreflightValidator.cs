// File: src/PostgresCopy/Migration/DestinationPreflightValidator.cs

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
                errors.Add(
                    $"{plannedTable.QualifiedName}: destination table is missing." + Environment.NewLine +
                    $"  Origin side: the table exists in schema \"{plannedTable.Table.Schema}\" and was selected for this run." + Environment.NewLine +
                    $"  Destination side: create the same table in schema \"{plannedTable.Table.Schema}\", or use Create schema against the intended empty destination database.");
                continue;
            }

            if (!ColumnsMatch(plannedTable.Table.Columns, destinationTable.Columns))
            {
                errors.Add(
                    $"{plannedTable.QualifiedName}: destination columns do not match origin columns." + Environment.NewLine +
                    $"  Origin columns:      {FormatColumns(plannedTable.Table.Columns)}" + Environment.NewLine +
                    $"  Destination columns: {FormatColumns(destinationTable.Columns)}" + Environment.NewLine +
                    "  Origin side: confirm the Schema and Tables fields selected the source tables you intended." + Environment.NewLine +
                    "  Destination side: rebuild or migrate the destination schema so the table has the same columns in the same order.");
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(
                "Destination preflight failed before any data was copied." + Environment.NewLine +
                "What happened: origin and destination are reachable, but the destination schema does not match the origin schema for the planned tables." + Environment.NewLine +
                "Why PostgresCopy stopped: binary COPY requires each destination table to have the same columns in the same order. Continuing could fail halfway or put data into the wrong shape." + Environment.NewLine +
                "How to resolve: if the destination should be a fresh clone, point at the correct empty destination database and run Create schema. If the destination is an existing app database, apply the missing migrations or schema changes there first, then dry-run again." +
                Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, errors));
        }
    }

    private static bool ColumnsMatch(IReadOnlyList<string> originColumns, IReadOnlyList<string> destinationColumns)
    {
        return originColumns.SequenceEqual(destinationColumns, StringComparer.Ordinal);
    }

    private static string FormatColumns(IReadOnlyList<string> columns)
    {
        return columns.Count == 0
            ? "(none)"
            : string.Join(", ", columns.Select(column => $"\"{column}\""));
    }
}
