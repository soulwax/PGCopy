// File: tests/PostgresCopy.Tests/SchemaCreatorDropSchemaTests.cs

using PostgresCopy.Migration;

namespace PostgresCopy.Tests;

public sealed class SchemaCreatorDropSchemaTests
{
    [Fact]
    public async Task DropAndRecreateSchemaAsync_returns_error_message_when_psql_not_on_path()
    {
        // CheckToolAvailable shells out to psql --version. If psql is present in this
        // environment the guard passes and the method proceeds to connect — which will
        // fail for a different reason. We only test the guard path here; the actual
        // drop+create execution requires a running PostgreSQL and is covered by the
        // integration script (scripts/integration-test.ps1).
        var psqlAvailable = SchemaCreator.CheckToolAvailable("psql") is null;
        if (psqlAvailable)
        {
            // psql is on PATH — guard passes; skip rather than attempt a real connection.
            return;
        }

        var result = await SchemaCreator.DropAndRecreateSchemaAsync(
            "Host=localhost;Database=db",
            "public",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("psql", result);
        Assert.Contains("PATH", result);
    }

    [Fact]
    public void CheckToolAvailable_returns_null_for_dotnet()
    {
        // dotnet is always on PATH in this build environment.
        // Use it as a proxy to verify CheckToolAvailable returns null (= found) for a
        // real tool, independently of whether pg_dump / psql are installed.
        var error = SchemaCreator.CheckToolAvailable("dotnet");

        Assert.Null(error);
    }

    [Fact]
    public void CheckToolAvailable_returns_error_for_nonexistent_tool()
    {
        var error = SchemaCreator.CheckToolAvailable("this-tool-does-not-exist-pgcopy-test");

        Assert.NotNull(error);
        Assert.Contains("not found on PATH", error);
    }
}
