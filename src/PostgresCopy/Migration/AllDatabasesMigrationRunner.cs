// File: src/PostgresCopy/Migration/AllDatabasesMigrationRunner.cs

using Npgsql;
using PostgresCopy.Config;
using PostgresCopy.Database;
using PostgresCopy.Logging;

namespace PostgresCopy.Migration;

public sealed class AllDatabasesMigrationRunner(IMigrationLogger logger)
{
    public static IReadOnlyList<string> FilterSelectedDatabases(
        IReadOnlyList<string> allDatabases,
        IReadOnlyList<string> excludeDatabases)
    {
        var excludeSet = new HashSet<string>(excludeDatabases, StringComparer.Ordinal);
        return allDatabases.Where(name => !excludeSet.Contains(name)).ToList();
    }

    public static AllDatabasesRunResult BuildSummary(IReadOnlyList<PerDatabaseResult> results)
    {
        return new AllDatabasesRunResult(
            results.Count,
            results.Count(r => r.Succeeded),
            results.Count(r => !r.Succeeded),
            results);
    }

    public async Task<AllDatabasesRunResult> RunAsync(
        AllDatabasesMigrationSettings settings,
        bool destructiveActionsConfirmed,
        CancellationToken cancellationToken)
    {
        if (!settings.DryRun && !destructiveActionsConfirmed)
        {
            throw new ValidationException("Copy all databases was not confirmed. Migration cancelled.");
        }

        logger.Step("Enumerating origin databases");
        var allDatabases = await ListOriginDatabasesAsync(settings.OriginConnectionString, cancellationToken);
        var selected = FilterSelectedDatabases(allDatabases, settings.ExcludeDatabases);
        logger.Info($"Found {allDatabases.Count} database(s) on origin, {selected.Count} selected after exclusions.");

        if (!settings.DryRun)
        {
            logger.Step("Checking destination maintenance connection");
            var (reachable, failureReason) = await DestinationDatabaseLifecycle.TryOpenMaintenanceConnectionAsync(
                settings.DestinationConnectionString, cancellationToken);
            if (!reachable)
            {
                throw new ValidationException(failureReason ?? "Destination maintenance connection is not reachable.");
            }
            logger.Success("Destination maintenance connection reachable.");
        }

        var results = new List<PerDatabaseResult>();
        foreach (var databaseName in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var startedAt = DateTimeOffset.UtcNow;
            logger.DatabaseStart(databaseName);

            try
            {
                var result = await RunSingleDatabaseAsync(settings, databaseName, cancellationToken);
                var elapsed = DateTimeOffset.UtcNow - startedAt;
                logger.DatabaseDone(databaseName, elapsed);
                results.Add(new PerDatabaseResult(databaseName, true, result, null, elapsed));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var elapsed = DateTimeOffset.UtcNow - startedAt;
                logger.DatabaseFailed(databaseName, ex.Message);
                results.Add(new PerDatabaseResult(databaseName, false, null, ex.Message, elapsed));
            }
        }

        return BuildSummary(results);
    }

    private async Task<MigrationRunResult> RunSingleDatabaseAsync(
        AllDatabasesMigrationSettings settings,
        string databaseName,
        CancellationToken cancellationToken)
    {
        var originConnectionString = PostgresConnectionString.WithDatabase(settings.OriginConnectionString, databaseName);
        var destConnectionString = PostgresConnectionString.WithDatabase(settings.DestinationConnectionString, databaseName);

        if (!settings.DryRun)
        {
            await using var maintenanceConnection = await OpenMaintenanceConnectionAsync(
                settings.DestinationConnectionString, cancellationToken);

            var terminatedCount = await DestinationDatabaseLifecycle.TerminateOtherBackendsAsync(
                maintenanceConnection, databaseName, cancellationToken);
            if (terminatedCount > 0)
            {
                logger.Info($"Terminated {terminatedCount} other connection(s) to \"{databaseName}\" on destination.");
            }

            await DestinationDatabaseLifecycle.DropDatabaseAsync(maintenanceConnection, databaseName, cancellationToken);
            await DestinationDatabaseLifecycle.CreateDatabaseAsync(maintenanceConnection, databaseName, cancellationToken);

            var schemaError = await SchemaCreator.CreateAsync(
                originConnectionString, destConnectionString, "public", cancellationToken);
            if (schemaError is not null)
            {
                throw new ValidationException($"Schema creation failed for \"{databaseName}\": {schemaError}");
            }
        }

        var origin = PostgresConnectionString.Parse(originConnectionString);
        var destination = PostgresConnectionString.Parse(destConnectionString);

        var perDatabaseSettings = new MigrationSettings(
            origin,
            destination,
            "public",
            [],
            settings.DryRun,
            false,
            settings.Verify,
            settings.Verbose,
            settings.Yes,
            settings.BatchSize,
            false,
            false,
            false,
            false);

        return await new MigrationRunner(logger).RunAsync(perDatabaseSettings, destructiveActionsConfirmed: true, cancellationToken);
    }

    private static async Task<NpgsqlConnection> OpenMaintenanceConnectionAsync(
        string destinationConnectionString,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in new[] { DestinationDatabaseLifecycle.DefaultMaintenanceDatabase, "template1" })
        {
            var candidateConnectionString = PostgresConnectionString.WithDatabase(destinationConnectionString, candidate);
            var connection = new NpgsqlConnection(candidateConnectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                return connection;
            }
            catch (OperationCanceledException)
            {
                await connection.DisposeAsync();
                throw;
            }
            catch (Exception)
            {
                // Try the next candidate.
                await connection.DisposeAsync();
            }
        }

        throw new ValidationException(
            "Could not open a maintenance connection to the destination server to drop/create databases.");
    }

    private static async Task<IReadOnlyList<string>> ListOriginDatabasesAsync(
        string originConnectionString,
        CancellationToken cancellationToken)
    {
        var maintenanceConnectionString = PostgresConnectionString.WithDatabase(
            originConnectionString, DestinationDatabaseLifecycle.DefaultMaintenanceDatabase);

        await using var connection = new NpgsqlConnection(maintenanceConnectionString);
        await connection.OpenAsync(cancellationToken);
        return await DestinationDatabaseLifecycle.ListDatabasesAsync(connection, cancellationToken);
    }
}
