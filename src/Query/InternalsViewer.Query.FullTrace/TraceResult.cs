using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.CustomHooks;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.Memory;

namespace InternalsViewer.Query.FullTrace;

public sealed record TraceResult(CallStackTree CallStack, TraceCallStore CallStore, TraceTimeline Timeline) : IRunExtra
{
    public IteratorPlanLink PlanLink { get; init; } = IteratorPlanLink.Empty;

    internal IReadOnlyList<IteratorPlanNode> PlanNodes { get; init; } = [];

    internal IReadOnlyList<QueryTeardown> Teardowns { get; init; } = [];

    public HoBtAccessMap HoBtAccess { get; init; } = HoBtAccessMap.Empty;

    public MemoryIndex Memory { get; init; } = MemoryIndex.Empty;

    public TraceOperatorLifetimes Lifetimes { get; init; } = TraceOperatorLifetimes.Empty;

    public MemoryMap MemoryMap { get; init; } = MemoryMap.Empty;

    public ObjectPoolSnapshot ObjectPool { get; init; } = ObjectPoolSnapshot.Empty;

    public CustomHookStore CustomHooks { get; init; } = CustomHookStore.Empty;

    public MemoryHistoryStore MemoryHistory { get; init; } = MemoryHistoryStore.Empty;

    internal IReadOnlyList<TraceOutputStructure> OutputStructures { get; init; } = [];

    public TraceRowReturnMarks RowReturnMarks { get; init; } = TraceRowReturnMarks.Empty;

    public TraceTickSeries Reads { get; init; } = TraceTickSeries.Empty;

    public TraceTickSeries Evaluations { get; init; } = TraceTickSeries.Empty;

    public IReadOnlyList<CallStackNode> QueryRoots => Roots ??= QueryExecutionCropper.GetRoots(CallStack);

    public TraceTimeline QueryTimeline => CroppedTimeline ??= GetQueryTimeline();

    public IReadOnlyList<TraceOutputRowSource> OutputSources => Sources ??= TraceOutputLineage.GetSources(Lifetimes, OutputStructures);

    private IReadOnlyList<CallStackNode>? Roots { get; set; }

    private IReadOnlyList<TraceOutputRowSource>? Sources { get; set; }

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
