// File: src/PostgresCopy/Migration/DestinationDataPreflight.cs

using Npgsql;
using PostgresCopy.Config;
using PostgresCopy.Logging;

namespace PostgresCopy.Migration;

public sealed class DestinationDataPreflight(
    NpgsqlConnection destination,
    IMigrationLogger logger)
{
    public async Task ValidateAsync(MigrationPlan plan, CancellationToken cancellationToken)
    {
        if (plan.TruncateDestination)
        {
            logger.Info("Destination data preflight: truncate selected.");
            return;
        }

        var rowCounts = new List<TableRowCount>();
        foreach (var tablePlan in plan.Tables)
        {
            var rows = await TableRowCounter.CountAsync(destination, tablePlan.Table, cancellationToken);
            rowCounts.Add(new TableRowCount(tablePlan.QualifiedName, rows));
        }

        ValidateEmptyDestinationRows(rowCounts);
        logger.Success("Destination tables are empty.");
    }

    public static void ValidateEmptyDestinationRows(IReadOnlyList<TableRowCount> rowCounts)
    {
        var nonEmptyTables = rowCounts
            .Where(rowCount => rowCount.Rows > 0)
            .ToArray();

        if (nonEmptyTables.Length == 0)
        {
            return;
        }

        var lines = nonEmptyTables.Select(rowCount =>
            $"{rowCount.TableName}: destination has {rowCount.Rows} row(s).");
        var message = "Destination contains data. Refusing to append into non-empty tables."
            + Environment.NewLine
            + "What happened: the origin and destination schemas are compatible, but at least one planned destination table already has rows."
            + Environment.NewLine
            + "Why PostgresCopy stopped: appending a full database copy into existing rows can create duplicates, foreign-key conflicts, or mixed old/new data."
            + Environment.NewLine
            + string.Join(Environment.NewLine, lines)
            + Environment.NewLine
            + "How to resolve: on the origin side, confirm the selected tables are the tables you meant to copy. On the destination side, either choose an empty database/table set, or use Truncate destination in the desktop app / --truncate-destination with confirmation in the CLI to replace the existing rows.";

        throw new ValidationException(message);
    }
}
