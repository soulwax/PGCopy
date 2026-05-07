// File: src/PostgresCopy/Database/DatabasePeekInspector.cs

using Npgsql;
using PostgresCopy.Config;

namespace PostgresCopy.Database;

public sealed class DatabasePeekInspector
{
    public const string DatabasesSql = """
        select datname
        from pg_database
        where datallowconn
          and not datistemplate
        order by datname;
        """;

    public const string UserTablesSql = """
        select
            table_schema,
            table_name
        from information_schema.tables
        where table_type = 'BASE TABLE'
          and table_schema <> 'pg_catalog'
          and table_schema <> 'information_schema'
          and table_schema not like 'pg_toast%'
        order by table_schema, table_name;
        """;

    public async Task<DatabasePeekResult> PeekAsync(
        string databaseUrl,
        CancellationToken cancellationToken)
    {
        var connectionInfo = PostgresConnectionString.ParseForInspection(databaseUrl);

        await using var connection = new NpgsqlConnection(connectionInfo.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        if (!connectionInfo.HasRequestedDatabase)
        {
            var databases = await GetDatabasesAsync(connection, cancellationToken);
            return new DatabasePeekResult(
                connectionInfo.RedactedConnectionString,
                null,
                databases,
                []);
        }

        var tables = await GetTableCountsAsync(connection, cancellationToken);
        return new DatabasePeekResult(
            connectionInfo.RedactedConnectionString,
            connectionInfo.RequestedDatabase,
            [],
            tables);
    }

    private static async Task<IReadOnlyList<string>> GetDatabasesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var databases = new List<string>();

        await using var command = new NpgsqlCommand(DatabasesSql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            databases.Add(reader.GetString(0));
        }

        return databases;
    }

    private static async Task<IReadOnlyList<TablePeekResult>> GetTableCountsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var tables = new List<TableInfo>();

        await using (var command = new NpgsqlCommand(UserTablesSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(new TableInfo(
                    reader.GetString(0),
                    reader.GetString(1),
                    []));
            }
        }

        var tableCounts = new List<TablePeekResult>(tables.Count);
        foreach (var table in tables)
        {
            var rowCount = await CountTableRowsAsync(connection, table, cancellationToken);
            tableCounts.Add(new TablePeekResult(table.Schema, table.Name, rowCount));
        }

        return tableCounts;
    }

    private static async Task<long> CountTableRowsAsync(
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
