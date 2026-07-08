namespace InternalsViewer.Query.Results;

public sealed class QueryResultSet
{
    public IReadOnlyList<ResultColumn> Columns { get; init; } = [];

    public IReadOnlyList<ResultRow> Rows { get; init; } = [];
}
