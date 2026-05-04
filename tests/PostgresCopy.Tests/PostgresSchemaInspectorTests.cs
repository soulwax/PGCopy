// File: tests/PostgresCopy.Tests/PostgresSchemaInspectorTests.cs

using PostgresCopy.Database;

namespace PostgresCopy.Tests;

public sealed class PostgresSchemaInspectorTests
{
    [Fact]
    public void UserTablesSql_filters_to_requested_schema_and_base_tables()
    {
        Assert.Contains("table_type = 'BASE TABLE'", PostgresSchemaInspector.UserTablesSql);
        Assert.Contains("table_schema = @schema", PostgresSchemaInspector.UserTablesSql);
    }

    [Fact]
    public void ColumnsSql_orders_by_ordinal_position()
    {
        Assert.Contains("order by ordinal_position", PostgresSchemaInspector.ColumnsSql);
    }

    [Fact]
    public void ForeignKeyDependenciesSql_reads_foreign_key_relationships()
    {
        Assert.Contains("constraint_type = 'FOREIGN KEY'", PostgresSchemaInspector.ForeignKeyDependenciesSql);
        Assert.Contains("ccu.table_name as depends_on_table_name", PostgresSchemaInspector.ForeignKeyDependenciesSql);
    }
}
