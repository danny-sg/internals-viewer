using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.MemoryObject;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.Memory;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.FullTrace;

public sealed record TraceResult(CallTree CallTree, TraceCallStore CallStore, TraceTimeline Timeline) : IRunExtra
{
    internal const int ActivityBuckets = 96;

    public IteratorPlanLink PlanLink { get; init; } = IteratorPlanLink.Empty;

    public OperatorFrameMap OperatorFrames { get; init; } = OperatorFrameMap.Empty;

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

    public TraceRowReturnMarkMap RowReturnMarks { get; init; } = TraceRowReturnMarkMap.Empty;

    public TraceTickSeries RowAccesses { get; init; } = TraceTickSeries.Empty;

    public TraceTickSeries PageHeaderAccesses { get; init; } = TraceTickSeries.Empty;

    public TraceTickSeries PageFetches { get; init; } = TraceTickSeries.Empty;

    public TraceTickSeries Seeks { get; init; } = TraceTickSeries.Empty;

    public TraceTickSeries Opens => field ??= TraceOperatorOpenTrackBuilder.Build(Timeline, Lifetimes);

    public TraceHashTableMap HashTables => field ??= new TraceHashTableMap(this);

    public IReadOnlyList<CallTreeNode> QueryRoots => Roots ??= QueryExecutionCropper.GetRoots(CallTree);

    public TraceTimeline QueryTimeline => CroppedTimeline ??= GetQueryTimeline();

    internal TraceTickSeries SeekRanges => field ??= TraceSeekRangeTrackBuilder.Build(Timeline, CallStore, Lifetimes, EnclosingCalls);

    internal TraceTickSeries SeekKeys => field ??= TraceSeekRangeTrackBuilder.BuildKeys(Timeline, CallStore, Lifetimes, EnclosingCalls);

    internal EnclosingOperatorCallIndex PullerCalls
        => field ??= EnclosingOperatorCallIndex.Build(CallStore,
                                                      Lifetimes.OperatorsByInstance
                                                               .Where(o => !OperatorClassifier.IsComputeScalar(o.Value))
                                                               .ToDictionary(o => o.Key, o => o.Value));

    internal EnclosingOperatorCallIndex EnclosingCalls
    {
        get => field ??= EnclosingOperatorCallIndex.Build(CallStore, Lifetimes.OperatorsByInstance);
        init;
    }

    private IReadOnlyList<CallTreeNode>? Roots { get; set; }

    private TraceTimeline? CroppedTimeline { get; set; }

    private TraceTimeline? ActivityWindow { get; set; }

    private Dictionary<TraceTimeline, Dictionary<CallTreeNode, int[]>> ActivityByWindow { get; } =
        new(ReferenceEqualityComparer.Instance);

    public int GetSeekRange(TraceTick tick)
        => TraceSeekRangeTrackBuilder.GetRange(SeekRanges, CallStore, tick.Operator, tick.Thread, tick.Span.From.Position);

    public List<VariantLocator> GetSeekKeys(TraceTick tick)
        => TraceSeekRangeTrackBuilder.GetKeys(SeekKeys, CallStore, tick.Operator, tick.Thread, tick.Span.From.Position);

    internal TraceResult WithPageAccesses()
    {
        var accesses = TracePageAccessList.Build(CallStore, MemoryHistory, BufferPool, PageReads, Timeline);

        var rowAccesses = TraceRowAccessTrackBuilder.Build(Timeline, accesses, Lifetimes, EnclosingCalls);

        var headerAccesses = TraceRowAccessTrackBuilder.BuildHeaders(Timeline, accesses, Lifetimes, EnclosingCalls);

        var pageFetches = TracePageFetchTrackBuilder.Build(Timeline, accesses, BufferPool, Lifetimes, EnclosingCalls);

        return this with
        {
            PageAccesses = accesses,
            RowAccesses = rowAccesses,
            PageHeaderAccesses = headerAccesses,
            PageFetches = pageFetches
        };
    }

    internal IEnumerable<(string Component, long Items, long? Bytes)> GetComponentSizes()
    {
        yield return ("Timeline Spans", Timeline.SpanCount, Timeline.Bytes);
        yield return ("Call Log Calls", CallStore.CallCount, CallStore.Size);
        yield return ("Locator Entries", CallStore.LocatorCount, CallStore.LocatorTableBytes);
        yield return ("Unique Locators", CallStore.UniqueLocatorCount, CallStore.UniqueLocatorBytes);
        yield return ("Memory Map Entries", AddressMap.Count, AddressMap.Bytes);
        yield return ("Memory Allocations", Allocation.AllocationCount, Allocation.Bytes);
        yield return ("Memory History Writes", MemoryHistory.WriteCount, MemoryHistory.WriteBytes);
        yield return ("Memory History Reads", MemoryHistory.ReadCount, MemoryHistory.ReadBytes);
        yield return ("Buffer Pool Pages", BufferPool.Count, BufferPool.Bytes);
        yield return ("Page Reads", PageReads.Count, PageReads.Bytes);
        yield return ("Page Accesses", PageAccesses.Count, PageAccesses.Bytes);
        yield return ("Call Tree Nodes", CallTree.GetNodes().LongCount(), null);
        yield return ("Operator Lifetimes", Lifetimes.Count, null);
        yield return ("Row Access Ticks", RowAccesses.Count, RowAccesses.Bytes);
        yield return ("Page Header Access Ticks", PageHeaderAccesses.Count, PageHeaderAccesses.Bytes);
        yield return ("Page Fetch Ticks", PageFetches.Count, PageFetches.Bytes);
        yield return ("Seek Ticks", Seeks.Count, Seeks.Bytes);
        yield return ("Open Ticks", Opens.Count, Opens.Bytes);
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

        CallTree.SetCallActivity(() => GetCallActivity(window));

        CallTree.ComputeActivity(ActivityBuckets);
    }

    private Dictionary<CallTreeNode, int[]> GetCallActivity(TraceTimeline window)
    {
        lock (ActivityByWindow)
        {
            if (!ActivityByWindow.TryGetValue(window, out var activity))
            {
                activity = window.GetCallActivity(ActivityBuckets);

                ActivityByWindow[window] = activity;
            }

            return activity;
        }
    }

    private TraceTimeline GetQueryTimeline()
        => QueryRoots.Count > 0
            ? Timeline.CropTo(QueryRoots.ToHashSet<CallTreeNode>(ReferenceEqualityComparer.Instance)) ?? Timeline
            : Timeline;
}
