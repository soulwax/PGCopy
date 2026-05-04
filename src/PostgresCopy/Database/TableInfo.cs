// File: src/PostgresCopy/Database/TableInfo.cs

namespace PostgresCopy.Database;

public sealed record TableInfo(
    string Schema,
    string Name,
    IReadOnlyList<string> Columns)
{
    public string QualifiedName => SqlIdentifier.Qualify(Schema, Name);
}
