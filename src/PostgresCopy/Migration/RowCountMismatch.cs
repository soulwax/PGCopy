// File: src/PostgresCopy/Migration/RowCountMismatch.cs

namespace PostgresCopy.Migration;

public sealed record RowCountMismatch(
    TableMigrationPlan Table,
    long OriginRows,
    long DestinationRows)
{
    public string QualifiedName => Table.QualifiedName;
}
