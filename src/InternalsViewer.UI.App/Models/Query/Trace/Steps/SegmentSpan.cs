using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.UI.App.Models.Query.Trace.Steps;

public sealed record SegmentSpan() : AccessStep(AccessPhase.Segment), ITraceSpan
{
    public SegmentProgress Progress { get; } = new();

    public bool IsComplete { get; set; }
}

public sealed partial class SegmentProgress : ObservableObject
{
    [ObservableProperty]
    private long _rows;

    [ObservableProperty]
    private long _segments;

    [ObservableProperty]
    private string _key = "";

    [ObservableProperty]
    private bool _hasKey;

    public void Apply(AccessStep.SegmentRow step)
    {
        Rows = step.Number;
        Segments = step.SegmentCount;
        Key = step.Key;

        HasKey = step.Key.Length > 0;
    }
}
