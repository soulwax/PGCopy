// File: src/PostgresCopy/Migration/RowCountVerifier.cs

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
        var mismatches = await FindMismatchesAsync(plan, cancellationToken);
        if (mismatches.Count > 0)
        {
            throw new VerificationException(BuildFailureMessage(mismatches));
        }
    }

    public async Task<IReadOnlyList<RowCountMismatch>> FindMismatchesAsync(
        MigrationPlan plan,
        CancellationToken cancellationToken)
    {
        logger.Step("Verifying row counts");
        var mismatches = new List<RowCountMismatch>();

        foreach (var tablePlan in plan.Tables)
        {
            var originCount = await TableRowCounter.CountAsync(origin, tablePlan.Table, cancellationToken);
            var destinationCount = await TableRowCounter.CountAsync(destination, tablePlan.Table, cancellationToken);

            if (originCount == destinationCount)
            {
                logger.Info($"{tablePlan.QualifiedName}: {destinationCount} row(s) verified.");
                continue;
            }

            logger.Info($"{tablePlan.QualifiedName}: origin has {originCount}, destination has {destinationCount}; repair needed.");
            mismatches.Add(new RowCountMismatch(tablePlan, originCount, destinationCount));
        }

        if (mismatches.Count == 0)
        {
            logger.Success("Row-count verification passed.");
        }

        return mismatches;
    }

    public static string BuildFailureMessage(IReadOnlyList<RowCountMismatch> mismatches)
    {
        var errors = mismatches.Select(mismatch =>
            $"{mismatch.QualifiedName}: origin has {mismatch.OriginRows}, destination has {mismatch.DestinationRows}.");
        return "Row-count verification failed:" + Environment.NewLine + string.Join(Environment.NewLine, errors);
    }
}
