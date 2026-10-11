namespace InternalsViewer.Query.Results;

public sealed class ResultRow<T>(object?[] values)
{
    public T? Id { get; set; }

    public ResultRow() : this([])
    {
    }

    public ResultRow(IResultRowSource source, int row) : this([])
    {
        Source = source;
        Row = row;
    }

    public object? this[int ordinal] => Source is null ? values[ordinal] : Source.Value(Row, ordinal);

    public int FieldCount => Source?.FieldCount ?? values.Length;

    private IResultRowSource? Source { get; }

    private int Row { get; }
}
