namespace InternalsViewer.Query.Plans.Model;

public sealed record MergeInfo
{
    public List<ColumnReference> OuterKeys { get; init; } = [];

    public List<ColumnReference> InnerKeys { get; init; } = [];

    public bool ManyToMany { get; init; }
}
