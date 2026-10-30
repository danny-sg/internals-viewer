using Windows.Foundation;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed class MemoryReferenceGraphEdge(Point[] points,
                                             Point labelAt,
                                             string label,
                                             string changes,
                                             string detail,
                                             int changeCount,
                                             bool isCrossLink,
                                             bool isLabelBelow)
{
    public Point[] Points { get; } = points;

    public Point LabelAt { get; } = labelAt;

    public string Label { get; } = label;

    public string Changes { get; } = changes;

    public string Detail { get; } = detail;

    public int ChangeCount { get; } = changeCount;

    public bool IsCrossLink { get; } = isCrossLink;

    public bool IsLabelBelow { get; } = isLabelBelow;

    public bool IsHot => ChangeCount > 0;
}
