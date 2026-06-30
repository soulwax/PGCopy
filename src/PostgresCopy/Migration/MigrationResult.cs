// File: src/PostgresCopy/Migration/MigrationResult.cs

namespace PostgresCopy.Migration;

public sealed record MigrationResult(int TablesCopied, long RowsCopied)
{
    public static MigrationResult Empty { get; } = new(0, 0);

    public MigrationResult Add(MigrationResult other) =>
        new(TablesCopied + other.TablesCopied, RowsCopied + other.RowsCopied);
}
