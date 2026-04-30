using Npgsql;

namespace PostgresCopy.Database;

public sealed class PostgresSchemaInspector(NpgsqlConnection connection)
{
    public const string UserTablesSql = """
        select
            table_schema,
            table_name
        from information_schema.tables
        where table_type = 'BASE TABLE'
          and table_schema = @schema
        order by table_schema, table_name;
        """;

    public const string ColumnsSql = """
        select column_name
        from information_schema.columns
        where table_schema = @schema
          and table_name = @table
        order by ordinal_position;
        """;

    public const string ForeignKeyDependenciesSql = """
        select
            tc.table_name,
            ccu.table_name as depends_on_table_name
        from information_schema.table_constraints tc
        join information_schema.constraint_column_usage ccu
          on ccu.constraint_schema = tc.constraint_schema
         and ccu.constraint_name = tc.constraint_name
        where tc.constraint_type = 'FOREIGN KEY'
          and tc.table_schema = @schema
          and ccu.table_schema = @schema
        order by tc.table_name, ccu.table_name;
        """;

    public async Task<IReadOnlyList<TableInfo>> GetUserTablesAsync(
        string schema,
        IReadOnlyList<string> tableFilter,
        CancellationToken cancellationToken)
    {
        var tables = new List<TableInfo>();

        await using var tableCommand = new NpgsqlCommand(UserTablesSql, connection);
        tableCommand.Parameters.AddWithValue("schema", schema);

        await using var reader = await tableCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var tableSchema = reader.GetString(0);
            var tableName = reader.GetString(1);

            if (tableFilter.Count > 0 && !tableFilter.Contains(tableName, StringComparer.Ordinal))
            {
                continue;
            }

            tables.Add(new TableInfo(tableSchema, tableName, Array.Empty<string>()));
        }

        await reader.DisposeAsync();

        var tablesWithColumns = new List<TableInfo>(tables.Count);
        foreach (var table in tables)
        {
            var columns = await GetColumnsAsync(table.Schema, table.Name, cancellationToken);
            tablesWithColumns.Add(table with { Columns = columns });
        }

        return tablesWithColumns;
    }

    public async Task<IReadOnlyList<TableDependency>> GetForeignKeyDependenciesAsync(
        string schema,
        IReadOnlyList<string> plannedTables,
        CancellationToken cancellationToken)
    {
        var plannedTableSet = plannedTables.ToHashSet(StringComparer.Ordinal);
        var dependencies = new List<TableDependency>();

        await using var command = new NpgsqlCommand(ForeignKeyDependenciesSql, connection);
        command.Parameters.AddWithValue("schema", schema);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var tableName = reader.GetString(0);
            var dependsOnTableName = reader.GetString(1);

            if (plannedTableSet.Contains(tableName) && plannedTableSet.Contains(dependsOnTableName))
            {
                dependencies.Add(new TableDependency(tableName, dependsOnTableName));
            }
        }

        return dependencies;
    }

    private async Task<IReadOnlyList<string>> GetColumnsAsync(
        string schema,
        string table,
        CancellationToken cancellationToken)
    {
        var columns = new List<string>();

        await using var command = new NpgsqlCommand(ColumnsSql, connection);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("table", table);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }
}
