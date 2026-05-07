// File: src/PostgresCopy/Config/MigrationSettingsValidator.cs

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
            throw new ValidationException(
                "Origin and destination point to the same database. Refusing to continue." + Environment.NewLine +
                "What happened: after normalizing the connection strings, both sides resolve to the same PostgreSQL database." + Environment.NewLine +
                "Why PostgresCopy stopped: copying a database into itself could truncate or rewrite the same data you are reading from." + Environment.NewLine +
                "How to resolve: keep the origin field pointed at the source database, and change the destination field to a separate target database. Pay special attention to host, port, database name, and SSH tunnel settings.");
        }

        if (string.IsNullOrWhiteSpace(options.Schema))
        {
            throw new ValidationException(
                "Schema cannot be empty." + Environment.NewLine +
                "What happened: PostgresCopy needs a PostgreSQL schema name before it can discover tables." + Environment.NewLine +
                "How to resolve: enter the source schema to copy, usually public.");
        }

        return new MigrationSettings(
            origin,
            destination,
            options.Schema,
            options.Tables,
            options.DryRun,
            options.TruncateDestination,
            options.Verify,
            options.Verbose,
            options.Yes,
            options.BatchSize,
            options.CreateSchema);
    }
}
