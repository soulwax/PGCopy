// File: src/PostgresCopy/Migration/MigrationResult.cs

namespace PostgresCopy.Migration;

public sealed record MigrationResult(int TablesCopied, long RowsCopied);
