using Npgsql;
using PostgresCopy.Database;
using PostgresCopy.Logging;

namespace PostgresCopy.Migration;

public sealed class DestinationTableCleaner(NpgsqlConnection destination, IMigrationLogger logger)
{
    public async Task TruncateAsync(MigrationPlan plan, CancellationToken cancellationToken)
    {
        if (plan.Tables.Count == 0)
        {
            return;
        }

        var tableList = string.Join(", ", plan.Tables.Select(table =>
            SqlIdentifier.Qualify(table.Table.Schema, table.Table.Name)));
        var sql = $"truncate table {tableList};";

        logger.Step("Truncating destination tables");
        logger.Info($"Tables: {plan.Tables.Count}");

        await using var transaction = await destination.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, destination, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.Success("Destination tables truncated.");
    }
}
