// File: tests/PostgresCopy.Tests/DestinationDataPreflightTests.cs

using PostgresCopy.Config;
using PostgresCopy.Migration;

namespace PostgresCopy.Tests;

public sealed class DestinationDataPreflightTests
{
    [Fact]
    public void ValidateEmptyDestinationRows_passes_when_all_tables_are_empty()
    {
        DestinationDataPreflight.ValidateEmptyDestinationRows([
            new TableRowCount("\"public\".\"accounts\"", 0),
            new TableRowCount("\"public\".\"orders\"", 0)
        ]);
    }

    [Fact]
    public void ValidateEmptyDestinationRows_fails_when_any_table_has_rows()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            DestinationDataPreflight.ValidateEmptyDestinationRows([
                new TableRowCount("\"public\".\"accounts\"", 0),
                new TableRowCount("\"public\".\"orders\"", 3)
            ]));

        Assert.Contains("Refusing to append", ex.Message);
        Assert.Contains("\"public\".\"orders\"", ex.Message);
        Assert.Contains("--truncate-destination", ex.Message);
    }
}
