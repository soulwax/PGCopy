using System.Threading.Channels;
using PostgresCopy.Logging;
using PostgresCopy.Migration;

namespace PostgresCopy.Web;

public sealed class StreamingMigrationLogger(ChannelWriter<MigrationEvent> writer) : IMigrationLogger
{
    public void Info(string message) => Write("info", message);

    public void Step(string message) => Write("step", message);

    public void Plan(MigrationPlan plan)
    {
        var truncate = plan.TruncateDestination ? " Destination tables will be truncated first." : string.Empty;
        var verify = plan.Verify ? " Row counts will be verified." : string.Empty;
        Write("plan", $"Plan: {plan.Tables.Count} table(s) in schema {plan.Schema}.{truncate}{verify}");

        foreach (var table in plan.Tables)
        {
            Write("plan-item", $"{table.QualifiedName} ({table.Table.Columns.Count} column(s))", table.QualifiedName);
        }
    }

    public void TableStart(string tableName, long rows)
    {
        Write("table-start", $"Copying {tableName}", tableName, rows);
    }

    public void TableDone(string tableName, long rows, TimeSpan elapsed)
    {
        Write("table-done", $"Copied {tableName} in {elapsed.TotalSeconds:0.0}s", tableName, rows);
    }

    public void TableFailed(string tableName, string message)
    {
        Write("error", $"Failed {tableName}: {message}", tableName);
    }

    public void Success(string message) => Write("success", message);

    public void Error(string message) => Write("error", message);

    private void Write(string kind, string message, string? tableName = null, long? rows = null)
    {
        writer.TryWrite(new MigrationEvent(kind, message, tableName, rows));
    }
}
