// File: src/PostgresCopy/Migration/RowCountVerificationRepairer.cs

using Npgsql;
using PostgresCopy.Database;
using PostgresCopy.Logging;

namespace PostgresCopy.Migration;

public sealed class RowCountVerificationRepairer(
    NpgsqlConnection origin,
    NpgsqlConnection destination,
    IMigrationLogger logger)
{
    private const int MaxRepairAttempts = 3;
    private const int MaxLoggedTables = 8;

    public async Task<MigrationResult> VerifyAndRepairAsync(
        MigrationPlan plan,
        IReadOnlyList<TableDependency> dependencies,
        CancellationToken cancellationToken)
    {
        var repairTotal = MigrationResult.Empty;
        var verifier = new RowCountVerifier(origin, destination, logger);
        var mismatches = await verifier.FindMismatchesAsync(plan, cancellationToken);

        for (var attempt = 1; mismatches.Count > 0 && attempt <= MaxRepairAttempts; attempt++)
        {
            var repairPlan = CreateRepairPlan(plan, dependencies, mismatches);

            logger.Step($"Repairing row-count mismatch (attempt {attempt} of {MaxRepairAttempts})");
            logger.Info($"Mismatched table(s): {FormatTableList(mismatches.Select(mismatch => mismatch.QualifiedName))}");
            logger.Info($"Repair table set: {FormatTableList(repairPlan.Tables.Select(table => table.QualifiedName))}");
            logger.Info("Repair clears those destination table(s), recopies them from origin, then verifies counts again.");

            await new DestinationTableCleaner(destination, logger).TruncateAsync(repairPlan, cancellationToken);
            var repairResult = await new CopyDataMigrator(origin, destination, logger).CopyAsync(repairPlan, cancellationToken);
            repairTotal = repairTotal.Add(repairResult);

            mismatches = await verifier.FindMismatchesAsync(plan, cancellationToken);
        }

        if (mismatches.Count > 0)
        {
            throw new VerificationException(
                RowCountVerifier.BuildFailureMessage(mismatches)
                + Environment.NewLine
                + $"PostgresCopy retried mismatched table(s) {MaxRepairAttempts} time(s), but counts still changed or did not line up.");
        }

        return repairTotal;
    }

    public static MigrationPlan CreateRepairPlan(
        MigrationPlan plan,
        IReadOnlyList<TableDependency> dependencies,
        IReadOnlyList<RowCountMismatch> mismatches)
    {
        var plannedNames = plan.Tables
            .Select(table => table.Table.Name)
            .ToHashSet(StringComparer.Ordinal);
        var repairNames = mismatches
            .Select(mismatch => mismatch.Table.Table.Name)
            .Where(plannedNames.Contains)
            .ToHashSet(StringComparer.Ordinal);

        var changed = true;
        while (changed)
        {
            changed = false;

            foreach (var dependency in dependencies)
            {
                if (!plannedNames.Contains(dependency.TableName))
                {
                    continue;
                }

                if (repairNames.Contains(dependency.DependsOnTableName)
                    && repairNames.Add(dependency.TableName))
                {
                    changed = true;
                }
            }
        }

        var repairTables = plan.Tables
            .Where(table => repairNames.Contains(table.Table.Name))
            .ToArray();

        return plan with
        {
            DryRun = false,
            TruncateDestination = true,
            Tables = repairTables
        };
    }

    private static string FormatTableList(IEnumerable<string> names)
    {
        var tableNames = names.ToArray();
        var visible = string.Join(", ", tableNames.Take(MaxLoggedTables));

        if (tableNames.Length <= MaxLoggedTables)
        {
            return visible;
        }

        return $"{visible}, and {tableNames.Length - MaxLoggedTables} more";
    }
}
