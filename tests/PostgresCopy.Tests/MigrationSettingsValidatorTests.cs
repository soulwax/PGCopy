// File: tests/PostgresCopy.Tests/MigrationSettingsValidatorTests.cs

using PostgresCopy.Cli;
using PostgresCopy.Config;

namespace PostgresCopy.Tests;

public sealed class MigrationSettingsValidatorTests
{
    private static CliOptions ValidOptions(string origin = "Host=origin;Database=db", string dest = "Host=dest;Database=db") =>
        new(Origin: origin,
            Destination: dest,
            Schema: "public",
            Tables: [],
            DryRun: false,
            TruncateDestination: false,
            Verify: false,
            Verbose: false,
            Yes: false,
            BatchSize: 10000,
            CreateSchema: false,
            SchemaOnly: false,
            DataOnly: false,
            DropSchema: false);

    [Fact]
    public void Validate_returns_settings_for_distinct_databases()
    {
        var options = ValidOptions();

        var settings = MigrationSettingsValidator.Validate(options);

        Assert.Equal("public", settings.Schema);
    }

    [Fact]
    public void Validate_throws_when_origin_equals_destination()
    {
        var options = ValidOptions(
            origin: "Host=localhost;Port=5432;Database=app",
            dest: "Host=localhost;Port=5432;Database=app");

        var ex = Assert.Throws<ValidationException>(() => MigrationSettingsValidator.Validate(options));
        Assert.Contains("same database", ex.Message);
        Assert.Contains("How to resolve:", ex.Message);
    }

    [Fact]
    public void Validate_throws_when_origin_and_destination_are_same_via_url()
    {
        var options = ValidOptions(
            origin: "postgres://user:secret@localhost:5432/app",
            dest: "postgres://user:secret@localhost:5432/app");

        var ex = Assert.Throws<ValidationException>(() => MigrationSettingsValidator.Validate(options));
        Assert.Contains("same database", ex.Message);
    }

    [Fact]
    public void Validate_throws_when_schema_is_empty()
    {
        var options = ValidOptions() with { Schema = "" };

        var ex = Assert.Throws<ValidationException>(() => MigrationSettingsValidator.Validate(options));
        Assert.Contains("Schema cannot be empty", ex.Message);
        Assert.Contains("How to resolve:", ex.Message);
    }

    [Fact]
    public void Validate_throws_when_schema_is_whitespace()
    {
        var options = ValidOptions() with { Schema = "   " };

        var ex = Assert.Throws<ValidationException>(() => MigrationSettingsValidator.Validate(options));
        Assert.Contains("Schema cannot be empty", ex.Message);
    }

    [Fact]
    public void Validate_treats_same_host_different_database_as_distinct()
    {
        var options = ValidOptions(
            origin: "Host=localhost;Port=5432;Database=source",
            dest: "Host=localhost;Port=5432;Database=target");

        var settings = MigrationSettingsValidator.Validate(options);

        Assert.Equal("public", settings.Schema);
    }

    [Fact]
    public void Validate_treats_different_hosts_same_database_name_as_distinct()
    {
        var options = ValidOptions(
            origin: "Host=server1;Database=app",
            dest: "Host=server2;Database=app");

        var settings = MigrationSettingsValidator.Validate(options);

        Assert.NotNull(settings);
    }

    [Fact]
    public void Validate_propagates_tables_and_flags_to_settings()
    {
        var options = ValidOptions() with
        {
            Tables = ["orders", "users"],
            DryRun = true,
            Verify = true,
            TruncateDestination = true,
        };

        var settings = MigrationSettingsValidator.Validate(options);

        Assert.Equal(["orders", "users"], settings.TableFilter);
        Assert.True(settings.DryRun);
        Assert.True(settings.Verify);
        Assert.True(settings.TruncateDestination);
    }

    [Fact]
    public void Validate_requires_ssl_on_both_endpoints_by_default()
    {
        var options = ValidOptions(
            origin: "Host=origin;Database=db",
            dest: "Host=dest;Database=db");

        var settings = MigrationSettingsValidator.Validate(options);

        Assert.Contains("SSL Mode=Require", settings.Origin.ConnectionString);
        Assert.Contains("SSL Mode=Require", settings.Destination.ConnectionString);
    }

    [Fact]
    public void Validate_does_not_require_ssl_when_opted_out_per_endpoint()
    {
        var options = ValidOptions(
            origin: "Host=origin;Database=db",
            dest: "Host=dest;Database=db") with
        {
            OriginRequireSsl = false,
            DestinationRequireSsl = false,
        };

        var settings = MigrationSettingsValidator.Validate(options);

        Assert.DoesNotContain("SSL Mode=Require", settings.Origin.ConnectionString);
        Assert.DoesNotContain("SSL Mode=Require", settings.Destination.ConnectionString);
    }
}
