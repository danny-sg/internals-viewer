namespace InternalsViewer.Query.Results;

public sealed class ResultRow(object?[] values)
{
    public object? this[int ordinal] => values[ordinal];

    public int FieldCount => values.Length;
}
