// File: tests/PostgresCopy.Tests/RowCountVerificationRepairerTests.cs

using PostgresCopy.Database;
using PostgresCopy.Migration;

namespace PostgresCopy.Tests;

public sealed class RowCountVerificationRepairerTests
{
    [Fact]
    public void CreateRepairPlan_includes_mismatched_table_and_dependents()
    {
        var plan = CreatePlan(
            new TableInfo("public", "accounts", ["id"]),
            new TableInfo("public", "orders", ["id", "account_id"]),
            new TableInfo("public", "order_items", ["id", "order_id"]));
        var mismatches = new[]
        {
            new RowCountMismatch(plan.Tables[0], 10, 9)
        };
        var dependencies = new[]
        {
            new TableDependency("orders", "accounts"),
            new TableDependency("order_items", "orders")
        };

        var repairPlan = RowCountVerificationRepairer.CreateRepairPlan(plan, dependencies, mismatches);

        Assert.Equal(
            ["\"public\".\"accounts\"", "\"public\".\"orders\"", "\"public\".\"order_items\""],
            repairPlan.Tables.Select(table => table.QualifiedName));
        Assert.False(repairPlan.DryRun);
        Assert.True(repairPlan.TruncateDestination);
    }

    [Fact]
    public void CreateRepairPlan_keeps_child_only_repair_narrow()
    {
        var plan = CreatePlan(
            new TableInfo("public", "accounts", ["id"]),
            new TableInfo("public", "orders", ["id", "account_id"]));
        var mismatches = new[]
        {
            new RowCountMismatch(plan.Tables[1], 10, 9)
        };
        var dependencies = new[]
        {
            new TableDependency("orders", "accounts")
        };

        var repairPlan = RowCountVerificationRepairer.CreateRepairPlan(plan, dependencies, mismatches);

        Assert.Equal(["\"public\".\"orders\""], repairPlan.Tables.Select(table => table.QualifiedName));
    }

    private static MigrationPlan CreatePlan(params TableInfo[] tables) =>
        new(
            "public",
            tables.Select(table => new TableMigrationPlan(table)).ToArray(),
            DryRun: false,
            TruncateDestination: false,
            Verify: true);
}
