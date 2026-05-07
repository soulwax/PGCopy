// File: tests/PostgresCopy.Tests/DestinationPreflightValidatorTests.cs

using PostgresCopy.Config;
using PostgresCopy.Database;
using PostgresCopy.Migration;

namespace PostgresCopy.Tests;

public sealed class DestinationPreflightValidatorTests
{
    [Fact]
    public void Validate_passes_when_destination_table_and_columns_match()
    {
        var plan = CreatePlan(new TableInfo("public", "users", ["id", "email"]));
        var destinationTables = new[]
        {
            new TableInfo("public", "users", ["id", "email"])
        };

        new DestinationPreflightValidator().Validate(plan, destinationTables);
    }

    [Fact]
    public void Validate_fails_when_destination_table_is_missing()
    {
        var plan = CreatePlan(new TableInfo("public", "users", ["id", "email"]));

        var ex = Assert.Throws<ValidationException>(() =>
            new DestinationPreflightValidator().Validate(plan, []));

        Assert.Contains("destination table is missing", ex.Message);
        Assert.Contains("What happened", ex.Message);
        Assert.Contains("Destination side", ex.Message);
    }

    [Fact]
    public void Validate_fails_when_columns_do_not_match()
    {
        var plan = CreatePlan(new TableInfo("public", "users", ["id", "email"]));
        var destinationTables = new[]
        {
            new TableInfo("public", "users", ["id", "name"])
        };

        var ex = Assert.Throws<ValidationException>(() =>
            new DestinationPreflightValidator().Validate(plan, destinationTables));

        Assert.Contains("destination columns do not match", ex.Message);
        Assert.Contains("Origin columns", ex.Message);
        Assert.Contains("Destination columns", ex.Message);
        Assert.Contains("How to resolve", ex.Message);
    }

    [Fact]
    public void Validate_fails_when_plan_has_no_tables()
    {
        var plan = new MigrationPlan("public", [], false, false, false);

        var ex = Assert.Throws<ValidationException>(() =>
            new DestinationPreflightValidator().Validate(plan, []));

        Assert.Contains("No origin tables", ex.Message);
    }

    private static MigrationPlan CreatePlan(params TableInfo[] tables)
    {
        return new MigrationPlan(
            "public",
            tables.Select(table => new TableMigrationPlan(table)).ToArray(),
            false,
            false,
            false);
    }
}
