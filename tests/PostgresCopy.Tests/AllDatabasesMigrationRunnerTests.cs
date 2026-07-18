// File: tests/PostgresCopy.Tests/AllDatabasesMigrationRunnerTests.cs

using PostgresCopy.Cli;
using PostgresCopy.Config;
using PostgresCopy.Logging;
using PostgresCopy.Migration;
using Xunit;

namespace PostgresCopy.Tests;

public class AllDatabasesMigrationRunnerTests
{
    private sealed class NullMigrationLogger : IMigrationLogger
    {
        public void Info(string message) { }
        public void Step(string message) { }
        public void Plan(MigrationPlan plan) { }
        public void TableStart(string tableName, long rows) { }
        public void TableDone(string tableName, long rows, TimeSpan elapsed) { }
        public void TableFailed(string tableName, string message) { }
        public void DatabaseStart(string databaseName) { }
        public void DatabaseDone(string databaseName, TimeSpan elapsed) { }
        public void DatabaseFailed(string databaseName, string message) { }
        public void Success(string message) { }
        public void Error(string message) { }
    }

    [Fact]
    public void SameServer_returns_true_when_host_and_port_match_but_database_differs()
    {
        var result = AllDatabasesMigrationRunner.SameServer(
            "postgres://user@host:5432/pgcopy",
            "postgres://user@host:5432/scratch");

        Assert.True(result);
    }

    [Fact]
    public void SameServer_returns_false_for_different_hosts()
    {
        var result = AllDatabasesMigrationRunner.SameServer(
            "postgres://user@hostA:5432/db",
            "postgres://user@hostB:5432/db");

        Assert.False(result);
    }

    [Fact]
    public async Task RunAsync_throws_ValidationException_when_origin_and_destination_share_server()
    {
        var settings = new AllDatabasesMigrationSettings(
            "postgres://user@host:5432/pgcopy",
            "postgres://user@host:5432/scratch",
            [],
            false,
            false,
            false,
            CliOptionsParser.DefaultBatchSize,
            false);

        var runner = new AllDatabasesMigrationRunner(new NullMigrationLogger());

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            runner.RunAsync(settings, destructiveActionsConfirmed: true, CancellationToken.None));

        Assert.Contains("same", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("server", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FilterSelectedDatabases_ExcludesRequestedNames()
    {
        var result = AllDatabasesMigrationRunner.FilterSelectedDatabases(
            allDatabases: ["app_db", "reporting_db", "staging_db"],
            excludeDatabases: ["staging_db"]);

        Assert.Equal(["app_db", "reporting_db"], result);
    }

    [Fact]
    public void FilterSelectedDatabases_ExcludeIsCaseSensitive()
    {
        var result = AllDatabasesMigrationRunner.FilterSelectedDatabases(
            allDatabases: ["App_Db"],
            excludeDatabases: ["app_db"]);

        Assert.Equal(["App_Db"], result);
    }

    [Fact]
    public void BuildSummary_CountsSucceededAndFailed_AndPreservesOrder()
    {
        var results = new List<PerDatabaseResult>
        {
            new("db1", true, new MigrationRunResult(false, 2, 100), null, TimeSpan.FromSeconds(1)),
            new("db2", false, null, "connection refused", TimeSpan.FromSeconds(2)),
            new("db3", true, new MigrationRunResult(false, 1, 5), null, TimeSpan.FromSeconds(1)),
        };

        var summary = AllDatabasesMigrationRunner.BuildSummary(results);

        Assert.Equal(3, summary.TotalDatabases);
        Assert.Equal(2, summary.Succeeded);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(["db1", "db2", "db3"], summary.Results.Select(r => r.DatabaseName));
    }
}
