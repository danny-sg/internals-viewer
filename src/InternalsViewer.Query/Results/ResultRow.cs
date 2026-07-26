namespace InternalsViewer.Query.Results;

public sealed class ResultRow<T>
{
    public T Id { get; set; }

    private readonly object?[] _values;

    public ResultRow(object?[] values)
    {
        _values = values;
    }

    public ResultRow()
    {
        _values = [];
    }

    public object? this[int ordinal] => _values[ordinal];

    public int FieldCount => _values.Length;
}
