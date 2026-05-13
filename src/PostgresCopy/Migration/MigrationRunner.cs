// File: src/PostgresCopy/Migration/MigrationRunner.cs

using Npgsql;
using PostgresCopy.Config;
using PostgresCopy.Database;
using PostgresCopy.Logging;

namespace PostgresCopy.Migration;

public sealed class MigrationRunner(IMigrationLogger logger)
{
    public async Task<MigrationRunResult> RunAsync(
        MigrationSettings settings,
        bool destructiveActionsConfirmed,
        CancellationToken cancellationToken)
    {
        logger.Step("Validating connections");
        logger.Info($"Origin:      {settings.Origin.RedactedConnectionString}");
        logger.Info($"Destination: {settings.Destination.RedactedConnectionString}");

        if (settings.DataOnly)
        {
            logger.Info("Data-only mode: destination schema must already exist and match origin.");
        }

        if (settings.CreateSchema)
        {
            logger.Step("Creating destination schema");
            logger.Info("Running pg_dump --schema-only and applying to destination.");
            logger.Info("Note: use a direct (non-pooled) connection string if pg_dump fails to connect.");
            var schemaError = await SchemaCreator.CreateAsync(
                settings.Origin.ConnectionString,
                settings.Destination.ConnectionString,
                settings.Schema,
                cancellationToken);
            if (schemaError != null)
            {
                logger.Error(schemaError);
                return new MigrationRunResult(false, 0, 0);
            }
            logger.Success("Schema applied to destination.");

            if (settings.SchemaOnly)
            {
                logger.Success("Schema-only run complete. No table data was copied.");
                return new MigrationRunResult(false, 0, 0);
            }
        }

        await using var origin = new NpgsqlConnection(settings.Origin.ConnectionString);
        await using var destination = new NpgsqlConnection(settings.Destination.ConnectionString);

        await origin.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);

        logger.Step("Discovering origin tables");
        var originInspector = new PostgresSchemaInspector(origin);
        var tables = await originInspector.GetUserTablesAsync(
            settings.Schema,
            settings.TableFilter,
            cancellationToken);
        OriginTableSelectionValidator.Validate(settings.TableFilter, tables);

        var dependencies = await originInspector.GetForeignKeyDependenciesAsync(
            settings.Schema,
            tables.Select(table => table.Name).ToArray(),
            cancellationToken);

        var plan = new MigrationPlanner().CreatePlan(settings, tables, dependencies);

        logger.Step("Checking destination schema");
        var destinationInspector = new PostgresSchemaInspector(destination);
        var destinationTables = await destinationInspector.GetUserTablesAsync(
            settings.Schema,
            plan.Tables.Select(table => table.Table.Name).ToArray(),
            cancellationToken);

        new DestinationPreflightValidator().Validate(plan, destinationTables);
        logger.Success("Destination preflight passed.");

        logger.Plan(plan);

        if (settings.DryRun)
        {
            await new DryRunReporter(origin, destination, logger).ReportAsync(plan, cancellationToken);
            logger.Success("Dry run complete. No data was copied.");
            return new MigrationRunResult(true, 0, 0);
        }

        await new DestinationDataPreflight(destination, logger).ValidateAsync(plan, cancellationToken);

        if (settings.TruncateDestination)
        {
            if (!destructiveActionsConfirmed)
            {
                throw new ValidationException("Destination truncate was not confirmed. Migration cancelled.");
            }

            await new DestinationTableCleaner(destination, logger).TruncateAsync(plan, cancellationToken);
        }

        logger.Step("Copying data");
        var result = await new CopyDataMigrator(origin, destination, logger).CopyAsync(plan, cancellationToken);

        if (settings.Verify)
        {
            await new RowCountVerifier(origin, destination, logger).VerifyAsync(plan, cancellationToken);
        }

        logger.Success($"Copied {result.TablesCopied} table(s), {result.RowsCopied} row(s).");
        return new MigrationRunResult(false, result.TablesCopied, result.RowsCopied);
    }
}
