namespace PostgresCopy.Database;

public static class SqlIdentifier
{
    public static string Quote(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentException("Identifier cannot be empty.", nameof(identifier));
        }

        return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    public static string Qualify(string schema, string name) => $"{Quote(schema)}.{Quote(name)}";
}
