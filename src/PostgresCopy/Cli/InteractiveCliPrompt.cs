// File: src/PostgresCopy/Cli/InteractiveCliPrompt.cs

namespace PostgresCopy.Cli;

public static class InteractiveCliPrompt
{
    public static string[] FillMissingRequiredOptions(string[] args)
    {
        if (Console.IsInputRedirected)
        {
            return args;
        }

        var completedArgs = args.ToList();

        if (!HasOption(args, "--origin"))
        {
            var origin = PromptRequired("Origin PostgreSQL URL");
            completedArgs.Add("--origin");
            completedArgs.Add(origin);
        }

        if (!HasOption(args, "--destination"))
        {
            var destination = PromptRequired("Destination PostgreSQL URL");
            completedArgs.Add("--destination");
            completedArgs.Add(destination);
        }

        return completedArgs.ToArray();
    }

    private static bool HasOption(string[] args, string option)
    {
        return args.Contains(option, StringComparer.OrdinalIgnoreCase);
    }

    private static string PromptRequired(string label)
    {
        while (true)
        {
            Console.Write($"{label}: ");
            var value = Console.ReadLine();

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }

            Console.WriteLine("Value is required.");
        }
    }
}
