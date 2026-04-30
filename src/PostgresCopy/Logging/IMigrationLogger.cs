using PostgresCopy.Migration;

namespace PostgresCopy.Logging;

public interface IMigrationLogger
{
    void Info(string message);
    void Step(string message);
    void Plan(MigrationPlan plan);
    void TableStart(string tableName, long rows);
    void TableDone(string tableName, long rows, TimeSpan elapsed);
    void TableFailed(string tableName, string message);
    void Success(string message);
    void Error(string message);
}
