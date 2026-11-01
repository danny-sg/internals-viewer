using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed partial class MemoryReferenceGraphEdge(MemoryReferenceGraphNode from,
                                                     MemoryReferenceGraphNode to,
                                                     Point[] points,
                                                     Point labelAt,
                                                     string label,
                                                     string widestLabel,
                                                     string changes,
                                                     string detail,
                                                     int changeCount,
                                                     bool isCrossLink,
                                                     bool isLabelBelow) : ObservableObject
{
    [ObservableProperty]
    private string _label = label;

    public MemoryReferenceGraphNode From { get; } = from;

    public MemoryReferenceGraphNode To { get; } = to;

    public Point[] Points { get; } = points;

    public Point LabelAt { get; } = labelAt;

    public string WidestLabel { get; } = widestLabel;

    public string Changes { get; } = changes;

    public string Detail { get; } = detail;

    public int ChangeCount { get; } = changeCount;

    public bool IsCrossLink { get; } = isCrossLink;

    public bool IsLabelBelow { get; } = isLabelBelow;

    public bool IsHot => ChangeCount > 0;
}
