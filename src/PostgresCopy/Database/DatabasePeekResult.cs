// File: src/PostgresCopy/Database/DatabasePeekResult.cs

namespace PostgresCopy.Database;

public sealed record DatabasePeekResult(
    string RedactedConnectionString,
    string? DatabaseName,
    IReadOnlyList<string> Databases,
    IReadOnlyList<TablePeekResult> Tables)
{
    public bool HasDatabase => DatabaseName is not null;
}

public sealed record TablePeekResult(
    string Schema,
    string Name,
    long RowCount);
