namespace InternalsViewer.Query.Results;

public sealed class ResultRow<T>(object?[] values)
{
    public T? Id { get; set; }

    public ResultRow() : this([])
    {
    }

    public object? this[int ordinal] => values[ordinal];

    public int FieldCount => values.Length;
}
