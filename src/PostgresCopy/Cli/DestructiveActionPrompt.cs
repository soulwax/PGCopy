namespace PostgresCopy.Cli;

public static class DestructiveActionPrompt
{
    public static bool ConfirmTruncateDestination(bool yes)
    {
        if (yes)
        {
            return true;
        }

        if (Console.IsInputRedirected)
        {
            return false;
        }

        Console.WriteLine("This will truncate the planned destination tables before copying.");
        Console.Write("Type TRUNCATE to continue: ");
        var response = Console.ReadLine();

        return string.Equals(response, "TRUNCATE", StringComparison.Ordinal);
    }
}
