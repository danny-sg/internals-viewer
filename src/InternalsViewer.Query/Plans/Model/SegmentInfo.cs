namespace InternalsViewer.Query.Plans.Model;

public sealed record SegmentInfo
{
    public List<ColumnReference> GroupBy { get; init; } = [];

    public ColumnReference? SegmentColumn { get; init; }
}
