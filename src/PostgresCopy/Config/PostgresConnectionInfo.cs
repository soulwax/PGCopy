// File: src/PostgresCopy/Config/PostgresConnectionInfo.cs

namespace PostgresCopy.Config;

public sealed record PostgresConnectionInfo(
    string ConnectionString,
    string RedactedConnectionString,
    string? RequestedDatabase,
    bool HasRequestedDatabase);
