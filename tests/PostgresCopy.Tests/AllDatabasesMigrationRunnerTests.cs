// File: tests/PostgresCopy.Tests/AllDatabasesMigrationRunnerTests.cs

using PostgresCopy.Migration;
using Xunit;

namespace PostgresCopy.Tests;

public class AllDatabasesMigrationRunnerTests
{
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
