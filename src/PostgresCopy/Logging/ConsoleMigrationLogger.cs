// File: src/PostgresCopy/Logging/ConsoleMigrationLogger.cs

using PostgresCopy.Migration;

namespace PostgresCopy.Logging;

public sealed class ConsoleMigrationLogger : IMigrationLogger
{
    public void Info(string message) => Console.WriteLine(message);

    public void Step(string message)
    {
        Console.WriteLine();
        Console.WriteLine($"> {message}");
    }

    public void Plan(MigrationPlan plan)
    {
        Step("Migration plan");
        Console.WriteLine($"Schema: {plan.Schema}");
        Console.WriteLine($"Mode:   {(plan.DryRun ? "dry run" : "copy data")}");
        Console.WriteLine($"Truncate destination: {(plan.TruncateDestination ? "yes" : "no")}");
        Console.WriteLine($"Verify row counts: {(plan.Verify ? "yes" : "no")}");
        Console.WriteLine($"Tables: {plan.Tables.Count}");

        foreach (var table in plan.Tables)
        {
            var columnSummary = table.Table.Columns.Count == 0
                ? "all columns"
                : $"{table.Table.Columns.Count} column(s)";

            Console.WriteLine($"  - {table.QualifiedName} ({columnSummary})");
        }
    }

    public void TableStart(string tableName, long rows)
    {
        Console.WriteLine($"Copying {tableName} ({rows} row(s))...");
    }

    public void TableDone(string tableName, long rows, TimeSpan elapsed)
    {
        Console.WriteLine($"Copied  {tableName} ({rows} row(s) in {elapsed.TotalSeconds:0.0}s).");
    }

    public void TableFailed(string tableName, string message)
    {
        Error($"Failed {tableName}: {message}");
    }

    public void Success(string message)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ForegroundColor = previousColor;
    }

    public void Error(string message)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ForegroundColor = previousColor;
    }
}
