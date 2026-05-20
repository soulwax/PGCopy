// File: tests/PostgresCopy.Tests/SchemaCreatorStripCreateSchemaTests.cs

using PostgresCopy.Migration;

namespace PostgresCopy.Tests;

public sealed class SchemaCreatorStripCreateSchemaTests
{
    [Fact]
    public void Strips_unquoted_create_schema_public()
    {
        var dump = "CREATE SCHEMA public;\nCREATE TABLE public.t (id int);\n";

        var stripped = SchemaCreator.StripCreateSchemaStatements(dump, "public");

        Assert.DoesNotContain("CREATE SCHEMA public;", stripped);
        Assert.Contains("CREATE TABLE public.t", stripped);
    }

    [Fact]
    public void Strips_quoted_create_schema()
    {
        var dump = "CREATE SCHEMA \"my_schema\";\nCREATE TABLE \"my_schema\".\"t\" (id int);\n";

        var stripped = SchemaCreator.StripCreateSchemaStatements(dump, "my_schema");

        Assert.DoesNotContain("CREATE SCHEMA \"my_schema\";", stripped);
        Assert.Contains("CREATE TABLE \"my_schema\".\"t\"", stripped);
    }

    [Fact]
    public void Leaves_create_schema_for_other_schema_alone()
    {
        var dump = "CREATE SCHEMA other;\nCREATE TABLE public.t (id int);\n";

        var stripped = SchemaCreator.StripCreateSchemaStatements(dump, "public");

        Assert.Contains("CREATE SCHEMA other;", stripped);
    }

    [Fact]
    public void Does_not_strip_create_table_or_create_index()
    {
        var dump = "CREATE TABLE public.t (id int);\nCREATE INDEX idx ON public.t (id);\n";

        var stripped = SchemaCreator.StripCreateSchemaStatements(dump, "public");

        Assert.Equal(dump, stripped);
    }

    [Fact]
    public void Handles_empty_input()
    {
        Assert.Equal(string.Empty, SchemaCreator.StripCreateSchemaStatements(string.Empty, "public"));
    }
}
