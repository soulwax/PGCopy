// File: src/PostgresCopy/Cli/DestructiveActionPrompt.cs

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

    public static bool ConfirmDropSchema(string schema, bool yes)
    {
        if (yes)
        {
            return true;
        }

        if (Console.IsInputRedirected)
        {
            return false;
        }

        Console.WriteLine($"This will DROP SCHEMA \"{schema}\" CASCADE on the destination.");
        Console.WriteLine("All tables, indexes, sequences, functions, views, and triggers in this schema will be permanently deleted.");
        Console.Write("Type DROP to continue: ");
        var response = Console.ReadLine();

        return string.Equals(response, "DROP", StringComparison.Ordinal);
    }

    public static bool ConfirmOverwriteAllDatabases(IReadOnlyList<string> databaseNames, bool yes)
    {
        if (yes)
        {
            return true;
        }

        if (Console.IsInputRedirected)
        {
            return false;
        }

        Console.WriteLine("This will DROP and recreate the following destination databases:");
        foreach (var name in databaseNames)
        {
            Console.WriteLine($"  {name}");
        }
        Console.WriteLine("Any other active connections to these databases will be forcibly terminated.");
        Console.WriteLine("All tables, indexes, sequences, functions, views, triggers, and data in each database will be permanently deleted before being recreated from origin.");
        Console.Write("Type OVERWRITE to continue: ");
        var response = Console.ReadLine();

        return string.Equals(response, "OVERWRITE", StringComparison.Ordinal);
    }
}
