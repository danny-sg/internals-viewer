namespace InternalsViewer.Query.Results;

public interface IResultRowSource
{
    int FieldCount { get; }

    object? Value(int row, int ordinal);
}
