// File: src/PostgresCopy/Migration/DryRunReporter.cs

using Npgsql;
using PostgresCopy.Logging;

namespace PostgresCopy.Migration;

public sealed class DryRunReporter(
    NpgsqlConnection origin,
    NpgsqlConnection destination,
    IMigrationLogger logger)
{
    public async Task ReportAsync(MigrationPlan plan, CancellationToken cancellationToken)
    {
        logger.Step("Dry-run details");
        logger.Info("Destination preflight: ready.");

        foreach (var tablePlan in plan.Tables)
        {
            var originCount = await TableRowCounter.CountAsync(origin, tablePlan.Table, cancellationToken);
            var destinationCount = await TableRowCounter.CountAsync(destination, tablePlan.Table, cancellationToken);
            var action = plan.TruncateDestination
                ? "destination rows would be truncated before copy"
                : "destination rows would remain before copy";

            logger.Info($"{tablePlan.QualifiedName}: origin {originCount} row(s), destination {destinationCount} row(s), {action}.");
        }
    }
}
