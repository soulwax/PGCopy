namespace PostgresCopy.Database;

public sealed record TableDependency(string TableName, string DependsOnTableName);
