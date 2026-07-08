namespace InternalsViewer.Query.Results;

public sealed record ResultColumn(int Ordinal, string Name, string DataTypeName, Type ClrType, bool IsNullable);
