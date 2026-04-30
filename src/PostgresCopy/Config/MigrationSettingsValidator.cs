using PostgresCopy.Cli;

namespace PostgresCopy.Config;

public static class MigrationSettingsValidator
{
    public static MigrationSettings Validate(CliOptions options)
    {
        var origin = PostgresConnectionString.Parse(options.Origin);
        var destination = PostgresConnectionString.Parse(options.Destination);

        if (origin.ComparisonKey.Equals(destination.ComparisonKey, StringComparison.Ordinal))
        {
            throw new ValidationException("Origin and destination point to the same database. Refusing to continue.");
        }

        if (string.IsNullOrWhiteSpace(options.Schema))
        {
            throw new ValidationException("Schema cannot be empty.");
        }

        return new MigrationSettings(
            origin,
            destination,
            options.Schema,
            options.Tables,
            options.DryRun,
            options.TruncateDestination,
            options.Verbose,
            options.Yes,
            options.BatchSize);
    }
}
