// File: src/PostgresCopy.Desktop/UiMigrationLogger.cs

using PostgresCopy.Logging;
using PostgresCopy.Migration;

namespace PostgresCopy.Desktop;

public sealed class UiMigrationLogger(Action<string> write) : IMigrationLogger
{
    public void Info(string message) => write(message);

    public void Step(string message) => write($"== {message}");

    public void Plan(MigrationPlan plan)
    {
        var truncate = plan.TruncateDestination ? " Destination tables will be truncated first." : string.Empty;
        var verify = plan.Verify ? " Row counts will be verified and mismatches repaired." : string.Empty;
        write($"Plan: {plan.Tables.Count} table(s) in schema {plan.Schema}.{truncate}{verify}");

        foreach (var table in plan.Tables)
        {
            write($"  - {table.QualifiedName} ({table.Table.Columns.Count} column(s))");
        }
    }

    public void TableStart(string tableName, long rows) => write($"Copying {tableName} ({rows} row(s))");

    public void TableDone(string tableName, long rows, TimeSpan elapsed)
    {
        write($"Copied {tableName}: {rows} row(s) in {elapsed.TotalSeconds:0.0}s");
    }

    public void TableFailed(string tableName, string message) => write($"Failed {tableName}: {message}");

    public void DatabaseStart(string databaseName) => write($"Database: {databaseName}");

    public void DatabaseDone(string databaseName, TimeSpan elapsed) =>
        write($"Database {databaseName} done in {elapsed.TotalSeconds:0.0}s");

    public void DatabaseFailed(string databaseName, string message) =>
        write($"ERROR Failed database {databaseName}: {message}");

    public void Success(string message) => write($"OK {message}");

    public void Error(string message) => write($"ERROR {message}");
}
