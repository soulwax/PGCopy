// File: tests/PostgresCopy.Tests/OriginTableSelectionValidatorTests.cs

using PostgresCopy.Config;
using PostgresCopy.Database;
using PostgresCopy.Migration;

namespace PostgresCopy.Tests;

public sealed class OriginTableSelectionValidatorTests
{
    [Fact]
    public void Validate_passes_when_requested_tables_exist()
    {
        OriginTableSelectionValidator.Validate(
            ["accounts"],
            [new TableInfo("public", "accounts", ["id"])]);
    }

    [Fact]
    public void Validate_fails_when_requested_table_is_missing()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            OriginTableSelectionValidator.Validate(
                ["accounts", "orders"],
                [new TableInfo("public", "accounts", ["id"])]));

        Assert.Contains("orders", ex.Message);
    }

    [Fact]
    public void Validate_fails_when_schema_has_no_tables()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            OriginTableSelectionValidator.Validate([], []));

        Assert.Contains("No origin tables", ex.Message);
    }
}
