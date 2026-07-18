// File: tests/PostgresCopy.Tests/CliOptionsParserAllDatabasesTests.cs

using PostgresCopy.Cli;
using Xunit;

namespace PostgresCopy.Tests;

public class CliOptionsParserAllDatabasesTests
{
    [Fact]
    public void Parse_AllDatabasesFlag_SetsOption()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "postgres://user:pass@localhost:5432/db",
            "--destination", "postgres://user:pass@localhost:5433/db",
            "--all-databases",
            "--yes",
        ]);

        Assert.True(result.Success);
        Assert.True(result.Options!.AllDatabases);
    }

    [Fact]
    public void Parse_ExcludeDatabaseRepeatable_CollectsAllValues()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "postgres://user:pass@localhost:5432/db",
            "--destination", "postgres://user:pass@localhost:5433/db",
            "--all-databases",
            "--exclude-database", "staging_db",
            "--exclude-database", "scratch_db",
            "--yes",
        ]);

        Assert.True(result.Success);
        Assert.Equal(["staging_db", "scratch_db"], result.Options!.ExcludeDatabases);
    }

    [Fact]
    public void Parse_AllDatabasesWithSchema_Fails()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "postgres://user:pass@localhost:5432/db",
            "--destination", "postgres://user:pass@localhost:5433/db",
            "--all-databases",
            "--schema", "custom",
            "--yes",
        ]);

        Assert.False(result.Success);
        Assert.Contains("--all-databases", result.ErrorMessage);
    }

    [Fact]
    public void Parse_AllDatabasesWithTables_Fails()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "postgres://user:pass@localhost:5432/db",
            "--destination", "postgres://user:pass@localhost:5433/db",
            "--all-databases",
            "--tables", "accounts",
            "--yes",
        ]);

        Assert.False(result.Success);
    }

    [Fact]
    public void Parse_AllDatabasesWithSchemaOnly_Fails()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "postgres://user:pass@localhost:5432/db",
            "--destination", "postgres://user:pass@localhost:5433/db",
            "--all-databases",
            "--schema-only",
            "--yes",
        ]);

        Assert.False(result.Success);
    }

    [Fact]
    public void Parse_AllDatabasesWithDataOnly_Fails()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "postgres://user:pass@localhost:5432/db",
            "--destination", "postgres://user:pass@localhost:5433/db",
            "--all-databases",
            "--data-only",
            "--yes",
        ]);

        Assert.False(result.Success);
    }
}
