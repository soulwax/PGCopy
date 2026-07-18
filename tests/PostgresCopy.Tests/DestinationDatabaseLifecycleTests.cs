using PostgresCopy.Database;
using Xunit;

namespace PostgresCopy.Tests;

public class DestinationDatabaseLifecycleTests
{
    [Fact]
    public void ExcludedDatabaseNames_ContainsSystemDatabasesOnly()
    {
        Assert.Equal(
            new[] { "template0", "template1", "postgres" },
            DestinationDatabaseLifecycle.ExcludedDatabaseNames);
    }

    [Theory]
    [InlineData("template0", true)]
    [InlineData("template1", true)]
    [InlineData("postgres", true)]
    [InlineData("app_db", false)]
    [InlineData("Template0", false)] // case-sensitive: PostgreSQL database names are case-sensitive by default
    public void IsExcludedSystemDatabase_MatchesOnlyExactSystemNames(string name, bool expectedExcluded)
    {
        Assert.Equal(expectedExcluded, DestinationDatabaseLifecycle.IsExcludedSystemDatabase(name));
    }
}
