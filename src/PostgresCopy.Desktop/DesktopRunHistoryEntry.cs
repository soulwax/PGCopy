// File: src/PostgresCopy.Desktop/DesktopRunHistoryEntry.cs

namespace PostgresCopy.Desktop;

internal sealed record DesktopRunHistoryEntry(
    DateTime StartedAtLocal,
    bool Succeeded,
    string Mode,
    string Origin,
    string Destination,
    string Schema,
    string Tables,
    int TablesCopied,
    long RowsCopied,
    TimeSpan Elapsed,
    string Message,
    string? BatchId = null);
