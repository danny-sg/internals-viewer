using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.MemoryObject;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.Memory;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

namespace InternalsViewer.Query.FullTrace;

internal sealed class TraceResultBuilder(CallTree callTree,
                                         TraceCallStore callStore,
                                         TraceTimeline timeline,
                                         IReadOnlyList<EngineEvent> events,
                                         IReadOnlyList<IteratorPlanNode> planNodes)
{
    public AllocationIndex Allocation { get; init; } = AllocationIndex.Empty;

    public AddressMap AddressMap { get; init; } = AddressMap.Empty;

    public IReadOnlyDictionary<ulong, MemoryObjectInfo> MemoryObjectSnapshot { get; init; } = new Dictionary<ulong, MemoryObjectInfo>();

    public ObjectPoolSnapshot ObjectPool { get; init; } = ObjectPoolSnapshot.Empty;

    public TraceBufferPool BufferPool { get; init; } = TraceBufferPool.Empty;

    public PlanNodeLayout PlanNodeLayout { get; init; } = PlanNodeLayout.SqlServer2025;

    public IteratorPlanLink PlanLink { get; private set; } = IteratorPlanLink.Empty;

    public OperatorFrameMap OperatorFrames { get; private set; } = OperatorFrameMap.Empty;

    public TraceOperatorLifetimeList Lifetimes { get; private set; } = TraceOperatorLifetimeList.Empty;

    public MemoryObjectMap MemoryObjects { get; private set; } = MemoryObjectMap.Empty;

    private CallTree CallTree { get; } = callTree;

    private TraceCallStore CallStore { get; } = callStore;

    private TraceTimeline Timeline { get; } = timeline;

    private IReadOnlyList<EngineEvent> Events { get; } = events;

    private IReadOnlyList<IteratorPlanNode> PlanNodes { get; } = planNodes;

    private IReadOnlyList<QueryTeardown> Teardowns { get; set; } = [];

    private bool IsLinked { get; set; }

    public void Link()
    {
        if (IsLinked)
        {
            return;
        }

        Teardowns = QueryTeardown.GetTeardowns(Timeline);

        Timeline.SetCalls(CallStore);

        PlanLink = IteratorPlanLinker.Link(PlanNodes, Events);

        OperatorFrames = OperatorFrameMap.Build(CallTree, Events, PlanLink);

        Lifetimes = IteratorLifetimeBuilder.Build(Timeline, Allocation, OperatorFrames.OperatorsByInstance, PlanNodes, Teardowns);

        MemoryObjects = MemoryObjectMap.Build(CallStore, Timeline.MappedNodes, MemoryObjectSnapshot);

        IsLinked = true;
    }

    public TraceResult Build(MemoryHistoryStore memoryHistory, TracePageReadList pageReads)
    {
        Link();

        var enclosing = EnclosingOperatorCallIndex.Build(CallStore, Lifetimes.OperatorsByInstance);

        var pageAccesses = TracePageAccessList.Build(CallStore, memoryHistory, BufferPool, pageReads, Timeline);

        var result = new TraceResult(CallTree, CallStore, Timeline)
        {
            PlanLink = PlanLink,
            OperatorFrames = OperatorFrames,
            PlanNodes = PlanOutputReader.Read(PlanNodes, memoryHistory, PlanNodeLayout),
            Teardowns = Teardowns,
            HoBtAccess = HoBtAccessMap.Build(CallStore, Timeline.MappedNodes),
            Allocation = Allocation,
            Lifetimes = Lifetimes,
            AddressMap = AddressMap,
            MemoryObjects = MemoryObjects,
            ObjectPool = ObjectPool,
            BufferPool = BufferPool,
            PageReads = pageReads,
            PageAccesses = pageAccesses,
            MemoryHistory = memoryHistory,
            SourceTracks = new TraceSourceTrackMap(Timeline, CallStore, Lifetimes)
            {
                EnclosingCalls = enclosing,
                RowReturns = TraceRowReturnMarkMap.Build(Timeline, CallStore, Lifetimes, enclosing),
                RowAccesses = TraceRowAccessTrackBuilder.Build(Timeline, CallStore, pageAccesses, BufferPool, Lifetimes, enclosing),
                PageHeaderAccesses = TraceRowAccessTrackBuilder.BuildHeaders(Timeline, pageAccesses, Lifetimes, enclosing),
                PageFetches = TracePageFetchTrackBuilder.Build(Timeline, pageAccesses, BufferPool, Lifetimes, enclosing),
                Seeks = TraceSeekTrackBuilder.Build(Timeline, CallStore, Lifetimes, enclosing)
            }
        };

        result.BuildOutputPaths();

        CallStore.ReleaseColumns();

        return result;
    }
}
