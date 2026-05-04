// File: tests/PostgresCopy.Tests/MigrationTableExceptionTests.cs

using PostgresCopy.Migration;

namespace PostgresCopy.Tests;

public sealed class MigrationTableExceptionTests
{
    [Fact]
    public void Constructor_keeps_partial_copy_summary()
    {
        var ex = new MigrationTableException(
            "\"public\".\"orders\"",
            "copy failed",
            new InvalidOperationException("copy failed"),
            2,
            42);

        Assert.Equal("\"public\".\"orders\"", ex.TableName);
        Assert.Equal(2, ex.TablesCopiedBeforeFailure);
        Assert.Equal(42, ex.RowsCopiedBeforeFailure);
        Assert.Contains("Failed while copying", ex.Message);
    }
}
