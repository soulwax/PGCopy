using Npgsql;
using PostgresCopy.Config;

namespace PostgresCopy.Database;

public static class DestinationDatabaseLifecycle
{
    public const string DefaultMaintenanceDatabase = "postgres";
    private const string FallbackMaintenanceDatabase = "template1";

    public static readonly IReadOnlyList<string> ExcludedDatabaseNames =
        ["template0", "template1", "postgres"];

    public static bool IsExcludedSystemDatabase(string databaseName) =>
        ExcludedDatabaseNames.Contains(databaseName, StringComparer.Ordinal);

    public const string ListDatabasesSql = """
        select datname
        from pg_database
        where datallowconn
          and not datistemplate
        order by datname;
        """;

    public static async Task<IReadOnlyList<string>> ListDatabasesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var databases = new List<string>();

        await using var command = new NpgsqlCommand(ListDatabasesSql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(0);
            if (!IsExcludedSystemDatabase(name))
            {
                databases.Add(name);
            }
        }

        return databases;
    }

    public static async Task<(bool Reachable, string? FailureReason)> TryOpenMaintenanceConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in new[] { DefaultMaintenanceDatabase, FallbackMaintenanceDatabase })
        {
            var candidateConnectionString = PostgresConnectionString.WithDatabase(connectionString, candidate);

            try
            {
                await using var connection = new NpgsqlConnection(candidateConnectionString);
                await connection.OpenAsync(cancellationToken);
                return (true, null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Try the next candidate maintenance database.
            }
        }

        return (false,
            $"Could not open a maintenance connection to either \"{DefaultMaintenanceDatabase}\" or \"{FallbackMaintenanceDatabase}\" on the destination server. " +
            "Copy all databases requires a reachable maintenance database to create and drop destination databases. " +
            "Check that the destination user has CONNECT privilege on one of these databases.");
    }

    public static async Task<int> TerminateOtherBackendsAsync(
        NpgsqlConnection maintenanceConnection,
        string targetDatabaseName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select pg_terminate_backend(pid)
            from pg_stat_activity
            where datname = @databaseName
              and pid <> pg_backend_pid();
            """;

        await using var command = new NpgsqlCommand(sql, maintenanceConnection);
        command.Parameters.AddWithValue("databaseName", targetDatabaseName);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var terminatedCount = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.GetBoolean(0))
            {
                terminatedCount++;
            }
        }

        return terminatedCount;
    }

    public static async Task DropDatabaseAsync(
        NpgsqlConnection maintenanceConnection,
        string targetDatabaseName,
        CancellationToken cancellationToken)
    {
        var sql = $"drop database if exists {SqlIdentifier.Quote(targetDatabaseName)};";
        await using var command = new NpgsqlCommand(sql, maintenanceConnection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task CreateDatabaseAsync(
        NpgsqlConnection maintenanceConnection,
        string targetDatabaseName,
        CancellationToken cancellationToken)
    {
        var sql = $"create database {SqlIdentifier.Quote(targetDatabaseName)};";
        await using var command = new NpgsqlCommand(sql, maintenanceConnection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
