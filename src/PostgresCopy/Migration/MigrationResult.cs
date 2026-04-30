namespace PostgresCopy.Migration;

public sealed record MigrationResult(int TablesCopied, long RowsCopied);
