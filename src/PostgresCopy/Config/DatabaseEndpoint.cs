namespace PostgresCopy.Config;

public sealed record DatabaseEndpoint(
    string ConnectionString,
    string RedactedConnectionString,
    string ComparisonKey);
