// File: tests/PostgresCopy.Tests/SqlIdentifierTests.cs

using PostgresCopy.Database;

namespace PostgresCopy.Tests;

public sealed class SqlIdentifierTests
{
    [Fact]
    public void Quote_wraps_identifier()
    {
        Assert.Equal("\"users\"", SqlIdentifier.Quote("users"));
    }

    [Fact]
    public void Quote_escapes_embedded_quotes()
    {
        Assert.Equal("\"a\"\"b\"", SqlIdentifier.Quote("a\"b"));
    }

    [Fact]
    public void Qualify_quotes_schema_and_table()
    {
        Assert.Equal("\"public\".\"users\"", SqlIdentifier.Qualify("public", "users"));
    }
}
