using Npgsql;
using PostgresCopy.Database;
using PostgresCopy.Logging;

namespace PostgresCopy.Migration;

public sealed class CopyDataMigrator(
    NpgsqlConnection origin,
    NpgsqlConnection destination,
    IMigrationLogger logger)
{
    public async Task<MigrationResult> CopyAsync(MigrationPlan plan, CancellationToken cancellationToken)
    {
        long totalRows = 0;
        var copiedTables = 0;

        foreach (var tablePlan in plan.Tables)
        {
            var rowCount = await TableRowCounter.CountAsync(origin, tablePlan.Table, cancellationToken);
            logger.TableStart(tablePlan.QualifiedName, rowCount);
            var startedAt = DateTimeOffset.UtcNow;

            try
            {
                await CopyTableAsync(tablePlan.Table, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.TableFailed(tablePlan.QualifiedName, ex.Message);
                throw new MigrationTableException(
                    tablePlan.QualifiedName,
                    ex.Message,
                    ex,
                    copiedTables,
                    totalRows);
            }

            totalRows += rowCount;
            copiedTables++;
            await new SequenceSynchronizer(destination, logger).SynchronizeAsync(tablePlan.Table, cancellationToken);
            logger.TableDone(tablePlan.QualifiedName, rowCount, DateTimeOffset.UtcNow - startedAt);
        }

        return new MigrationResult(copiedTables, totalRows);
    }

    private async Task CopyTableAsync(TableInfo table, CancellationToken cancellationToken)
    {
        var copyTarget = BuildCopyTarget(table);
        var exportSql = $"copy {copyTarget} to stdout (format binary);";
        var importSql = $"copy {copyTarget} from stdin (format binary);";

        await using var transaction = await destination.BeginTransactionAsync(cancellationToken);
        var exporter = await origin.BeginRawBinaryCopyAsync(exportSql, cancellationToken);
        var importer = await destination.BeginRawBinaryCopyAsync(importSql, cancellationToken);
        var copyCompleted = false;

        try
        {
            await exporter.CopyToAsync(importer, cancellationToken);
            await importer.FlushAsync(cancellationToken);
            copyCompleted = true;
            await importer.DisposeAsync();
            await exporter.DisposeAsync();
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (!copyCompleted)
            {
                await importer.CancelAsync();
                await exporter.CancelAsync();
            }

            await importer.DisposeAsync();
            await exporter.DisposeAsync();
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string BuildCopyTarget(TableInfo table)
    {
        var tableName = SqlIdentifier.Qualify(table.Schema, table.Name);
        if (table.Columns.Count == 0)
        {
            return tableName;
        }

        var columns = string.Join(", ", table.Columns.Select(SqlIdentifier.Quote));
        return $"{tableName} ({columns})";
    }
}
