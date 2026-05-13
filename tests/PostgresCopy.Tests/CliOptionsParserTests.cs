// File: tests/PostgresCopy.Tests/CliOptionsParserTests.cs

using PostgresCopy.Cli;

namespace PostgresCopy.Tests;

public sealed class CliOptionsParserTests
{
    [Fact]
    public void Parse_requires_origin()
    {
        var result = CliOptionsParser.Parse(["--destination", "Host=dest;Database=db"]);

        Assert.False(result.Success);
        Assert.Contains("--origin", result.ErrorMessage);
    }

    [Fact]
    public void Parse_accepts_minimal_copy_options()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "Host=origin;Database=db",
            "--destination", "Host=dest;Database=db"
        ]);

        Assert.True(result.Success);
        Assert.Equal("public", result.Options!.Schema);
        Assert.False(result.Options.DryRun);
        Assert.False(result.Options.TruncateDestination);
        Assert.False(result.Options.Verify);
    }

    [Fact]
    public void Parse_collects_table_filters()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "Host=origin;Database=db",
            "--destination", "Host=dest;Database=db",
            "--table", "users",
            "--tables", "orders,products"
        ]);

        Assert.True(result.Success);
        Assert.Equal(["users", "orders", "products"], result.Options!.Tables);
    }

    [Fact]
    public void Parse_accepts_explicit_truncate_destination()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "Host=origin;Database=db",
            "--destination", "Host=dest;Database=db",
            "--truncate-destination",
            "--yes"
        ]);

        Assert.True(result.Success);
        Assert.True(result.Options!.TruncateDestination);
        Assert.True(result.Options.Yes);
    }

    [Fact]
    public void Parse_accepts_verify()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "Host=origin;Database=db",
            "--destination", "Host=dest;Database=db",
            "--verify"
        ]);

        Assert.True(result.Success);
        Assert.True(result.Options!.Verify);
    }

    [Fact]
    public void Parse_accepts_schema_only()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "Host=origin;Database=db",
            "--destination", "Host=dest;Database=db",
            "--schema-only"
        ]);

        Assert.True(result.Success);
        Assert.True(result.Options!.SchemaOnly);
        Assert.True(result.Options.CreateSchema);
        Assert.False(result.Options.DataOnly);
    }

    [Fact]
    public void Parse_accepts_data_only()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "Host=origin;Database=db",
            "--destination", "Host=dest;Database=db",
            "--data-only"
        ]);

        Assert.True(result.Success);
        Assert.True(result.Options!.DataOnly);
        Assert.False(result.Options.CreateSchema);
        Assert.False(result.Options.SchemaOnly);
    }

    [Fact]
    public void Parse_rejects_schema_only_with_data_only()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "Host=origin;Database=db",
            "--destination", "Host=dest;Database=db",
            "--schema-only",
            "--data-only"
        ]);

        Assert.False(result.Success);
        Assert.Contains("cannot be used together", result.ErrorMessage);
    }

    [Fact]
    public void Parse_rejects_schema_only_with_dry_run()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "Host=origin;Database=db",
            "--destination", "Host=dest;Database=db",
            "--schema-only",
            "--dry-run"
        ]);

        Assert.False(result.Success);
        Assert.Contains("--schema-only", result.ErrorMessage);
    }

    [Fact]
    public void Parse_rejects_data_only_with_create_schema()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "Host=origin;Database=db",
            "--destination", "Host=dest;Database=db",
            "--data-only",
            "--create-schema"
        ]);

        Assert.False(result.Success);
        Assert.Contains("--data-only", result.ErrorMessage);
    }

    [Fact]
    public void Parse_rejects_drop_and_recreate_for_mvp()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "Host=origin;Database=db",
            "--destination", "Host=dest;Database=db",
            "--drop-and-recreate"
        ]);

        Assert.False(result.Success);
        Assert.Contains("destructive", result.ErrorMessage);
    }
}
