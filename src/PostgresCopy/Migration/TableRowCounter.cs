// File: src/PostgresCopy/Migration/TableRowCounter.cs

using Npgsql;
using PostgresCopy.Database;

namespace PostgresCopy.Migration;

public static class TableRowCounter
{
    public static async Task<long> CountAsync(
        NpgsqlConnection connection,
        TableInfo table,
        CancellationToken cancellationToken)
    {
        var sql = $"select count(*) from {SqlIdentifier.Qualify(table.Schema, table.Name)};";
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(value);
    }
}
