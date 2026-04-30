using PostgresCopy.Config;
using PostgresCopy.Database;
using PostgresCopy.Migration;

namespace PostgresCopy.Tests;

public sealed class MigrationPlannerTests
{
    [Fact]
    public void CreatePlan_preserves_table_order_and_dry_run()
    {
        var settings = new MigrationSettings(
            new DatabaseEndpoint("Host=origin;Database=db", "Host=origin;Database=db", "origin"),
            new DatabaseEndpoint("Host=dest;Database=db", "Host=dest;Database=db", "dest"),
            "public",
            [],
            true,
            false,
            false,
            10000);

        var tables = new[]
        {
            new TableInfo("public", "users", ["id", "email"]),
            new TableInfo("public", "orders", ["id", "user_id"])
        };

        var plan = new MigrationPlanner().CreatePlan(settings, tables);

        Assert.True(plan.DryRun);
        Assert.Equal(["public.users", "public.orders"], plan.Tables.Select(table => table.QualifiedName));
    }
}
