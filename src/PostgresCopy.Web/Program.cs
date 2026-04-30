using System.Text.Json;
using System.Threading.Channels;
using Npgsql;
using PostgresCopy.Cli;
using PostgresCopy.Config;
using PostgresCopy.Database;
using PostgresCopy.Logging;
using PostgresCopy.Migration;
using PostgresCopy.Web;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/migrations", async (HttpContext context) =>
{
    var request = await JsonSerializer.DeserializeAsync<MigrationRequest>(
        context.Request.Body,
        JsonOptions.Default,
        context.RequestAborted);

    context.Response.ContentType = "application/x-ndjson";
    context.Response.Headers.CacheControl = "no-store";

    var channel = Channel.CreateUnbounded<MigrationEvent>();
    var logger = new StreamingMigrationLogger(channel.Writer);

    var runTask = Task.Run(async () =>
    {
        try
        {
            if (request is null)
            {
                throw new ValidationException("Migration request is empty.");
            }

            await RunMigrationAsync(request, logger, context.RequestAborted);
        }
        catch (ValidationException ex)
        {
            logger.Error(ex.Message);
        }
        catch (MigrationTableException ex)
        {
            logger.Error(ex.Message);
        }
        catch (PostgresException ex)
        {
            logger.Error($"PostgreSQL error: {ex.MessageText}");
        }
        catch (OperationCanceledException)
        {
            logger.Error("Migration cancelled.");
        }
        catch (Exception ex)
        {
            logger.Error(ex.Message);
        }
        finally
        {
            channel.Writer.TryComplete();
        }
    }, context.RequestAborted);

    await foreach (var migrationEvent in channel.Reader.ReadAllAsync(context.RequestAborted))
    {
        await JsonSerializer.SerializeAsync(context.Response.Body, migrationEvent, JsonOptions.Default, context.RequestAborted);
        await context.Response.WriteAsync("\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }

    await runTask;
});

app.Run();

static async Task RunMigrationAsync(
    MigrationRequest request,
    IMigrationLogger logger,
    CancellationToken cancellationToken)
{
    var options = new CliOptions(
        request.Origin,
        request.Destination,
        string.IsNullOrWhiteSpace(request.Schema) ? "public" : request.Schema.Trim(),
        ParseTables(request.Tables),
        request.DryRun,
        request.TruncateDestination,
        false,
        false,
        CliOptionsParser.DefaultBatchSize);

    var settings = MigrationSettingsValidator.Validate(options);
    if (settings.TruncateDestination
        && !string.Equals(request.TruncateConfirmation, "TRUNCATE", StringComparison.Ordinal))
    {
        throw new ValidationException("Type TRUNCATE to confirm destination truncation.");
    }

    logger.Step("Validating connections");
    logger.Info($"Origin: {settings.Origin.RedactedConnectionString}");
    logger.Info($"Destination: {settings.Destination.RedactedConnectionString}");

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
        logger.Success("Dry run complete. No data was copied.");
        return;
    }

    if (settings.TruncateDestination)
    {
        await new DestinationTableCleaner(destination, logger).TruncateAsync(plan, cancellationToken);
    }

    logger.Step("Copying data");
    var result = await new CopyDataMigrator(origin, destination, logger).CopyAsync(plan, cancellationToken);
    logger.Success($"Copied {result.TablesCopied} table(s), {result.RowsCopied} row(s).");
}

static IReadOnlyList<string> ParseTables(string? tables)
{
    if (string.IsNullOrWhiteSpace(tables))
    {
        return [];
    }

    return tables
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
}
