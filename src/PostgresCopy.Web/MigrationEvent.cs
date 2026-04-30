namespace PostgresCopy.Web;

public sealed record MigrationEvent(
    string Kind,
    string Message,
    string? TableName = null,
    long? Rows = null);
