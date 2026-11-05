using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.MemoryObject;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History;
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

    public AllocationIndex Allocation { get; init; } = AllocationIndex.Empty;

    public TraceOperatorLifetimeList Lifetimes { get; init; } = TraceOperatorLifetimeList.Empty;

    public AddressMap AddressMap { get; init; } = AddressMap.Empty;

    public MemoryObjectMap MemoryObjects { get; init; } = MemoryObjectMap.Empty;

    public ObjectPoolSnapshot ObjectPool { get; init; } = ObjectPoolSnapshot.Empty;

    public TraceBufferPool BufferPool { get; init; } = TraceBufferPool.Empty;

    public TracePageReadList PageReads { get; init; } = TracePageReadList.Empty;

    public TracePageAccessList PageAccesses { get; init; } = TracePageAccessList.Empty;

    public MemoryHistoryStore MemoryHistory { get; init; } = MemoryHistoryStore.Empty;

    internal IReadOnlyList<TraceOutputStructure> OutputStructures { get; init; } = [];

    public TraceRowReturnMarkMap RowReturnMarks { get; init; } = TraceRowReturnMarkMap.Empty;

    public TraceTickSeries Reads { get; init; } = TraceTickSeries.Empty;

    public TraceTickSeries Evaluations { get; init; } = TraceTickSeries.Empty;

    public TraceTickSeries RowAccesses { get; init; } = TraceTickSeries.Empty;

    public TraceTickSeries Seeks { get; init; } = TraceTickSeries.Empty;

    public IReadOnlyList<CallStackNode> QueryRoots => Roots ??= QueryExecutionCropper.GetRoots(CallStack);

    public TraceTimeline QueryTimeline => CroppedTimeline ??= GetQueryTimeline();

    public IReadOnlyList<TraceOutputRowSource> OutputSources => Sources ??= TraceOutputLineage.GetSources(Lifetimes, OutputStructures);

    internal EnclosingOperatorCallIndex EnclosingCalls
    {
        get => field ??= EnclosingOperatorCallIndex.Build(CallStore, Lifetimes.OperatorsByInstance);
        init;
    }

    private IReadOnlyList<CallStackNode>? Roots { get; set; }

    private IReadOnlyList<TraceOutputRowSource>? Sources { get; set; }

    private TraceTimeline? CroppedTimeline { get; set; }

    private TraceTimeline? ActivityWindow { get; set; }

    private Dictionary<TraceTimeline, Dictionary<CallStackNode, int[]>> ActivityByWindow { get; } =
        new(ReferenceEqualityComparer.Instance);

    internal TraceResult WithPageAccesses()
    {
        var accesses = TracePageAccessList.Build(CallStore, MemoryHistory, BufferPool, PageReads, Timeline);

        var rowAccesses = TraceRowAccessReader.Build(Timeline, accesses, Lifetimes, EnclosingCalls);

        return this with { PageAccesses = accesses, RowAccesses = rowAccesses };
    }

    internal IEnumerable<(string Component, long Items, long? Bytes)> GetComponentSizes()
    {
        yield return ("Timeline Spans", Timeline.SpanCount, Timeline.Bytes);
        yield return ("Call Log Calls", CallStore.Calls, CallStore.Size);
        yield return ("Locator Entries", CallStore.LocatorCount, CallStore.LocatorTableBytes);
        yield return ("Unique Locators", CallStore.UniqueLocatorCount, CallStore.UniqueLocatorBytes);
        yield return ("Memory Map Entries", AddressMap.Count, AddressMap.Bytes);
        yield return ("Memory Allocations", Allocation.AllocationCount, Allocation.Bytes);
        yield return ("Memory History Writes", MemoryHistory.WriteCount, MemoryHistory.WriteBytes);
        yield return ("Memory History Reads", MemoryHistory.ReadCount, MemoryHistory.ReadBytes);
        yield return ("Buffer Pool Pages", BufferPool.Count, BufferPool.Bytes);
        yield return ("Page Reads", PageReads.Count, PageReads.Bytes);
        yield return ("Page Accesses", PageAccesses.Count, PageAccesses.Bytes);
        yield return ("Call Tree Nodes", CallStack.Nodes().LongCount(), null);
        yield return ("Operator Lifetimes", Lifetimes.Count, null);
        yield return ("Read Ticks", Reads.Count, Reads.Bytes);
        yield return ("Evaluation Ticks", Evaluations.Count, Evaluations.Bytes);
        yield return ("Row Access Ticks", RowAccesses.Count, RowAccesses.Bytes);
        yield return ("Seek Ticks", Seeks.Count, Seeks.Bytes);
        yield return ("Row Return Marks", RowReturnMarks.Count, RowReturnMarks.Ticks.Bytes);
        yield return ("HoBt Accesses", HoBtAccess.Count, null);
    }

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
