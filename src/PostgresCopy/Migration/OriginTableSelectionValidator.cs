using PostgresCopy.Config;
using PostgresCopy.Database;

namespace PostgresCopy.Migration;

public static class OriginTableSelectionValidator
{
    public static void Validate(
        IReadOnlyList<string> requestedTables,
        IReadOnlyList<TableInfo> discoveredTables)
    {
        if (requestedTables.Count == 0)
        {
            if (discoveredTables.Count == 0)
            {
                throw new ValidationException("No origin tables were found for the selected schema.");
            }

            return;
        }

        var discoveredNames = discoveredTables
            .Select(table => table.Name)
            .ToHashSet(StringComparer.Ordinal);
        var missingTables = requestedTables
            .Where(requestedTable => !discoveredNames.Contains(requestedTable))
            .ToArray();

        if (missingTables.Length == 0)
        {
            return;
        }

        throw new ValidationException("The origin database does not contain the requested table(s): "
            + string.Join(", ", missingTables));
    }
}
