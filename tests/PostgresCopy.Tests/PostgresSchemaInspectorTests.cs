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
}
