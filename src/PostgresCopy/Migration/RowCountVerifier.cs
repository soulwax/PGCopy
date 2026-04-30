using Npgsql;
using PostgresCopy.Logging;

namespace PostgresCopy.Migration;

public sealed class RowCountVerifier(
    NpgsqlConnection origin,
    NpgsqlConnection destination,
    IMigrationLogger logger)
{
    public async Task VerifyAsync(MigrationPlan plan, CancellationToken cancellationToken)
    {
        logger.Step("Verifying row counts");
        var errors = new List<string>();

        foreach (var tablePlan in plan.Tables)
        {
            var originCount = await TableRowCounter.CountAsync(origin, tablePlan.Table, cancellationToken);
            var destinationCount = await TableRowCounter.CountAsync(destination, tablePlan.Table, cancellationToken);

            if (originCount == destinationCount)
            {
                logger.Info($"{tablePlan.QualifiedName}: {destinationCount} row(s) verified.");
                continue;
            }

            errors.Add($"{tablePlan.QualifiedName}: origin has {originCount}, destination has {destinationCount}.");
        }

        if (errors.Count > 0)
        {
            throw new VerificationException("Row-count verification failed:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }

        logger.Success("Row-count verification passed.");
    }
}
