using PostgresCopy.Cli;
using Xunit;

namespace PostgresCopy.Tests;

public class DestructiveActionPromptTests
{
    [Fact]
    public void ConfirmOverwriteAllDatabases_ReturnsTrue_WhenYesFlagSet()
    {
        var result = DestructiveActionPrompt.ConfirmOverwriteAllDatabases(["app_db", "reporting_db"], yes: true);
        Assert.True(result);
    }
}
