using Npgsql;
using PostgresCopy.Config;
using Xunit;

namespace PostgresCopy.Tests;

public class PostgresConnectionStringWithDatabaseTests
{
    [Fact]
    public void WithDatabase_ReplacesDatabaseName_KeepsHostAndCredentials()
    {
        var result = PostgresConnectionString.WithDatabase(
            "postgres://user:secret@localhost:5432/original",
            "replacement");

        var builder = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("replacement", builder.Database);
        Assert.Equal("localhost", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("user", builder.Username);
        Assert.Equal("secret", builder.Password);
    }

    [Fact]
    public void WithDatabase_AcceptsNpgsqlKeywordFormat()
    {
        var result = PostgresConnectionString.WithDatabase(
            "Host=localhost;Port=5432;Username=user;Password=secret;Database=original",
            "replacement");

        var builder = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("replacement", builder.Database);
    }

    [Fact]
    public void WithDatabase_WorksWhenOriginalHasNoDatabase()
    {
        var result = PostgresConnectionString.WithDatabase(
            "postgres://user:secret@localhost:5432",
            "postgres");

        var builder = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("postgres", builder.Database);
    }

    [Fact]
    public void WithDatabase_ThrowsValidationException_ForEmptyDatabaseName()
    {
        Assert.Throws<ValidationException>(() =>
            PostgresConnectionString.WithDatabase(
                "postgres://user:secret@localhost:5432/original",
                ""));
    }
}
