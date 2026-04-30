namespace PostgresCopy.Migration;

public sealed class MigrationTableException(
    string tableName,
    string message,
    Exception innerException,
    int tablesCopiedBeforeFailure = 0,
    long rowsCopiedBeforeFailure = 0)
    : Exception($"Failed while copying {tableName}: {message}", innerException)
{
    public string TableName { get; } = tableName;

    public int TablesCopiedBeforeFailure { get; } = tablesCopiedBeforeFailure;

    public long RowsCopiedBeforeFailure { get; } = rowsCopiedBeforeFailure;
}
