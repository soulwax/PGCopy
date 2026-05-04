// File: src/PostgresCopy/Migration/TableRowCount.cs

namespace PostgresCopy.Migration;

public sealed record TableRowCount(string TableName, long Rows);
