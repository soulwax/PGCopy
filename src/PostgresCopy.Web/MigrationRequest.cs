namespace PostgresCopy.Web;

public sealed record MigrationRequest(
    string Origin,
    string Destination,
    string? Schema,
    string? Tables,
    bool DryRun,
    bool TruncateDestination,
    string? TruncateConfirmation);
