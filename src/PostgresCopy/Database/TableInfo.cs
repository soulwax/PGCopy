namespace PostgresCopy.Database;

public sealed record TableInfo(
    string Schema,
    string Name,
    IReadOnlyList<string> Columns)
{
    public string QualifiedName => $"{Schema}.{Name}";
}
