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
    public void ParseForInspection_accepts_url_without_database()
    {
        var connection = PostgresConnectionString.ParseForInspection("postgres://user:secret@localhost:5432");

        Assert.False(connection.HasRequestedDatabase);
        Assert.Null(connection.RequestedDatabase);
        Assert.Contains("Database=postgres", connection.ConnectionString);
        Assert.DoesNotContain("secret", connection.RedactedConnectionString);
    }

    [Fact]
    public void ParseForInspection_preserves_requested_database()
    {
        var connection = PostgresConnectionString.ParseForInspection("postgres://user:secret@localhost:5432/app");

        Assert.True(connection.HasRequestedDatabase);
        Assert.Equal("app", connection.RequestedDatabase);
        Assert.Contains("Database=app", connection.ConnectionString);
    }

    [Fact]
    public void Parse_rejects_unsupported_url_scheme_with_specific_message()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            PostgresConnectionString.Parse("https://localhost:5432/app"));

        Assert.Contains("Unsupported URL scheme 'https'", ex.Message);
        Assert.Contains("postgres://", ex.Message);
    }

    [Fact]
    public void Parse_rejects_postgres_url_without_database_with_specific_message()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            PostgresConnectionString.Parse("postgres://user:secret@localhost:5432"));

        Assert.Contains("missing a database name", ex.Message);
        Assert.Contains("my_database", ex.Message);
    }

    [Fact]
    public void Parse_rejects_postgres_url_without_host_with_specific_message()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            PostgresConnectionString.Parse("postgres:///app"));

        Assert.Contains("missing a host", ex.Message);
        Assert.Contains("server name", ex.Message);
    }

    [Fact]
    public void Parse_rejects_postgres_url_with_invalid_port_with_specific_message()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            PostgresConnectionString.Parse("postgres://user:secret@localhost:notaport/app"));

        Assert.Contains("invalid port 'notaport'", ex.Message);
        Assert.Contains("numeric PostgreSQL port", ex.Message);
    }

    [Fact]
    public void Parse_rejects_postgres_url_with_fragment_with_specific_message()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            PostgresConnectionString.Parse("postgres://user:sec%23ret@localhost:5432/app#ignored"));

        Assert.Contains("contains a fragment", ex.Message);
        Assert.Contains("%23", ex.Message);
    }

    [Fact]
    public void Parse_rejects_malformed_postgres_scheme_with_specific_message()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            PostgresConnectionString.Parse("postgres:/user:secret@localhost:5432/app"));

        Assert.Contains("missing '//'", ex.Message);
        Assert.Contains("postgres://", ex.Message);
    }

    [Fact]
    public void Parse_rejects_sslmode_typo_with_specific_message()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            PostgresConnectionString.Parse("postgres://user:secret@localhost:5432/app?sslmode=required"));

        Assert.Contains("invalid value for 'sslmode'", ex.Message);
        Assert.Contains("'required' is a common typo for 'require'", ex.Message);
        Assert.Contains("disable, allow, prefer, require, verify-ca, verify-full", ex.Message);
    }

    [Fact]
    public void Parse_accepts_valid_sslmode_require()
    {
        var endpoint = PostgresConnectionString.Parse("postgres://user:secret@localhost:5432/app?sslmode=require");

        Assert.Contains("SSL Mode=Require", endpoint.ConnectionString);
    }

    [Fact]
    public void Parse_accepts_percent_encoded_special_characters_in_url_password()
    {
        var endpoint = PostgresConnectionString.Parse("postgres://user:sec%40ret%23x@localhost:5432/app");

        Assert.Contains("Password=***", endpoint.RedactedConnectionString);
        Assert.DoesNotContain("sec@ret#x", endpoint.RedactedConnectionString);
    }

    [Fact]
    public void ParseForInspection_allows_postgres_url_without_database()
    {
        var connection = PostgresConnectionString.ParseForInspection("postgres://user:secret@localhost:5432");

        Assert.False(connection.HasRequestedDatabase);
        Assert.Equal("postgres", new Npgsql.NpgsqlConnectionStringBuilder(connection.ConnectionString).Database);
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
            false,
            false,
            false);

        var ex = Assert.Throws<ValidationException>(() => MigrationSettingsValidator.Validate(options));
        Assert.Contains("same database", ex.Message);
    }
}
