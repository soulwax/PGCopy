using Npgsql;
using PostgresCopy.Database;
using PostgresCopy.Logging;

namespace PostgresCopy.Migration;

public sealed class SequenceSynchronizer(NpgsqlConnection destination, IMigrationLogger logger)
{
    private const string SerialColumnsSql = """
        select column_name, pg_get_serial_sequence(@qualified_table, column_name)
        from information_schema.columns
        where table_schema = @schema
          and table_name = @table
          and pg_get_serial_sequence(@qualified_table, column_name) is not null
        order by ordinal_position;
        """;

    public async Task SynchronizeAsync(TableInfo table, CancellationToken cancellationToken)
    {
        var serialColumns = await GetSerialColumnsAsync(table, cancellationToken);
        foreach (var serialColumn in serialColumns)
        {
            var maxValue = await GetMaxValueAsync(table, serialColumn.ColumnName, cancellationToken);
            if (maxValue is null)
            {
                continue;
            }

            await using var command = new NpgsqlCommand(
                "select setval(cast(@sequence_name as regclass), @value, true);",
                destination);
            command.Parameters.AddWithValue("sequence_name", serialColumn.SequenceName);
            command.Parameters.AddWithValue("value", maxValue.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);

            logger.Info($"Sequence {serialColumn.SequenceName} set to {maxValue.Value}.");
        }
    }

    private async Task<IReadOnlyList<SerialColumn>> GetSerialColumnsAsync(
        TableInfo table,
        CancellationToken cancellationToken)
    {
        var columns = new List<SerialColumn>();
        var qualifiedTable = SqlIdentifier.Qualify(table.Schema, table.Name);

        await using var command = new NpgsqlCommand(SerialColumnsSql, destination);
        command.Parameters.AddWithValue("schema", table.Schema);
        command.Parameters.AddWithValue("table", table.Name);
        command.Parameters.AddWithValue("qualified_table", qualifiedTable);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(new SerialColumn(reader.GetString(0), reader.GetString(1)));
        }

        return columns;
    }

    private async Task<long?> GetMaxValueAsync(
        TableInfo table,
        string columnName,
        CancellationToken cancellationToken)
    {
        var sql = $"select max({SqlIdentifier.Quote(columnName)}) from {SqlIdentifier.Qualify(table.Schema, table.Name)};";
        await using var command = new NpgsqlCommand(sql, destination);
        var value = await command.ExecuteScalarAsync(cancellationToken);

        return value is null or DBNull ? null : Convert.ToInt64(value);
    }

    private sealed record SerialColumn(string ColumnName, string SequenceName);
}
