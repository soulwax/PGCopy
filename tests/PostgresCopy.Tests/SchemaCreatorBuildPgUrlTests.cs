// File: tests/PostgresCopy.Tests/SchemaCreatorBuildPgUrlTests.cs

using PostgresCopy.Config;
using PostgresCopy.Migration;

namespace PostgresCopy.Tests;

public sealed class SchemaCreatorBuildPgUrlTests
{
    [Fact]
    public void BuildPgUrl_throws_when_database_is_missing()
    {
        var connectionString = "Host=localhost;Port=5432;Username=user;Password=secret";

        var ex = Assert.Throws<ValidationException>(() => SchemaCreator.BuildPgUrl(connectionString));
        Assert.Contains("missing a database name", ex.Message);
        Assert.Contains("How to resolve:", ex.Message);
    }

    [Fact]
    public void BuildPgUrl_builds_postgresql_url_with_database()
    {
        var connectionString = "Host=db.example.com;Port=5433;Database=app;Username=user;Password=secret";

        var url = SchemaCreator.BuildPgUrl(connectionString);

        Assert.StartsWith("postgresql://", url);
        Assert.Contains("user:secret@db.example.com:5433/app", url);
    }

    [Fact]
    public void BuildPgUrl_percent_encodes_special_characters_in_password()
    {
        var connectionString = "Host=localhost;Database=app;Username=user;Password=sec@ret#x";

        var url = SchemaCreator.BuildPgUrl(connectionString);

        Assert.Contains("sec%40ret%23x", url);
        Assert.DoesNotContain("sec@ret#x", url);
    }
}
