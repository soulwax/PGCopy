namespace PostgresCopy.Web;

public sealed record MigrationRequest(
    string Origin,
    string Destination,
    string? Schema,
    string? Tables,
    bool DryRun,
    bool Verify,
    bool TruncateDestination,
    string? TruncateConfirmation);
