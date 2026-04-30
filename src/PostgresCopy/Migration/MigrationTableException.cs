namespace PostgresCopy.Migration;

public sealed class MigrationTableException(string tableName, string message, Exception innerException)
    : Exception($"Failed while copying {tableName}: {message}", innerException)
{
    public string TableName { get; } = tableName;
}
