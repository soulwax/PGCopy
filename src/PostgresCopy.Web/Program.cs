using System.Text.Json;
using System.Threading.Channels;
using Npgsql;
using PostgresCopy.Cli;
using PostgresCopy.Config;
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
            logger.Error($"Copied before failure: {ex.TablesCopiedBeforeFailure} table(s), {ex.RowsCopiedBeforeFailure} row(s).");
        }
        catch (VerificationException ex)
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
        request.Verify,
        false,
        false,
        CliOptionsParser.DefaultBatchSize,
        request.CreateSchema);

    var settings = MigrationSettingsValidator.Validate(options);
    if (settings.TruncateDestination
        && !string.Equals(request.TruncateConfirmation, "TRUNCATE", StringComparison.Ordinal))
    {
        throw new ValidationException("Type TRUNCATE to confirm destination truncation.");
    }

    await new MigrationRunner(logger).RunAsync(
        settings,
        destructiveActionsConfirmed: true,
        cancellationToken);
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
