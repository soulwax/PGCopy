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
    public void Parse_rejects_destructive_flags_for_mvp()
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
