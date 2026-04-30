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
            + string.Join(Environment.NewLine, lines)
            + Environment.NewLine
            + "Use --truncate-destination with confirmation to replace destination data.";

        throw new ValidationException(message);
    }
}
