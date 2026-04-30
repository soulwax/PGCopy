using PostgresCopy.Database;

namespace PostgresCopy.Migration;

public sealed record TableMigrationPlan(TableInfo Table)
{
    public string QualifiedName => Table.QualifiedName;
}
