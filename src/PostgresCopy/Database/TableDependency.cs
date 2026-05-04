// File: src/PostgresCopy/Database/TableDependency.cs

namespace PostgresCopy.Database;

public sealed record TableDependency(string TableName, string DependsOnTableName);
