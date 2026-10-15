using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace;

public sealed record TraceResult(CallStackTree CallStack, TraceCallStore CallStore, TraceTimeline Timeline) : IRunExtra
{
    public IteratorPlanLink PlanLink { get; init; } = IteratorPlanLink.Empty;

    public HoBtAccessMap HoBtAccess { get; init; } = HoBtAccessMap.Empty;

    public MemoryMap MemoryMap { get; init; } = MemoryMap.Empty;

    public RowBufferIndex RowBuffers { get; init; } = RowBufferIndex.Empty;

    public TraceRowBufferMarks RowBufferMarks { get; init; } = TraceRowBufferMarks.Empty;

    public IReadOnlyList<CallStackNode> QueryRoots => Roots ??= QueryExecutionCropper.GetRoots(CallStack);

    public TraceTimeline QueryTimeline => CroppedTimeline ??= GetQueryTimeline();

    private IReadOnlyList<CallStackNode>? Roots { get; set; }

    private TraceTimeline? CroppedTimeline { get; set; }

    private TraceTimeline? ActivityWindow { get; set; }

    private Dictionary<TraceTimeline, Dictionary<CallStackNode, int[]>> ActivityByWindow { get; } =
        new(ReferenceEqualityComparer.Instance);

    public void SetActivityWindow(TraceTimeline window)
    {
        if (ReferenceEquals(ActivityWindow, window))
        {
            return;
        }

        ActivityWindow = window;

        if (!ActivityByWindow.TryGetValue(window, out var activity))
        {
            activity = window.GetCallActivity(QueryRunner.ActivityBuckets);

            ActivityByWindow[window] = activity;
        }

        foreach (var node in CallStack.Nodes())
        {
            node.CallActivity = activity.GetValueOrDefault(node) ?? [];
        }

        CallStack.ComputeActivity(CallStack.ActivityMinUs, CallStack.ActivityMaxUs, QueryRunner.ActivityBuckets);
    }

    private TraceTimeline GetQueryTimeline()
        => QueryRoots.Count > 0
            ? Timeline.CroppedTo(QueryRoots.ToHashSet<CallStackNode>(ReferenceEqualityComparer.Instance)) ?? Timeline
            : Timeline;
}
