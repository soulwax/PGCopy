// File: tests/PostgresCopy.Tests/PostgresConnectionStringTests.cs

using PostgresCopy.Cli;
using PostgresCopy.Config;

namespace PostgresCopy.Tests;

public sealed class PostgresConnectionStringTests
{
    [Fact]
    public void Parse_accepts_postgres_url()
    {
        var endpoint = PostgresConnectionString.Parse("postgres://user:secret@localhost:5432/app");

        Assert.Contains("Host=localhost", endpoint.ConnectionString);
        Assert.Contains("Database=app", endpoint.ConnectionString);
        Assert.Contains("Username=user", endpoint.ConnectionString);
    }

    [Fact]
    public void Parse_redacts_password()
    {
        var endpoint = PostgresConnectionString.Parse("postgres://user:secret@localhost:5432/app");

        Assert.DoesNotContain("secret", endpoint.RedactedConnectionString);
        Assert.Contains("Password=***", endpoint.RedactedConnectionString);
    }

    [Fact]
    public void Validate_rejects_identical_origin_and_destination()
    {
        var options = new CliOptions(
            "postgres://user:secret@localhost:5432/app",
            "Host=localhost;Port=5432;Database=app;Username=user;Password=other",
            "public",
            [],
            false,
            false,
            false,
            false,
            false,
            CliOptionsParser.DefaultBatchSize,
            false);

        var ex = Assert.Throws<ValidationException>(() => MigrationSettingsValidator.Validate(options));
        Assert.Contains("same database", ex.Message);
    }
}
