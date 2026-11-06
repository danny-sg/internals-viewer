using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.MemoryObject;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.Memory;

namespace InternalsViewer.Query.FullTrace;

internal sealed class TraceResultBuilder
{
    public TraceResultBuilder(CallTree callTree,
                              TraceCallStore callStore,
                              TraceTimeline timeline,
                              IReadOnlyList<EngineEvent> events,
                              IReadOnlyList<IteratorPlanNode> planNodes)
    {
        CallTree = callTree;
        CallStore = callStore;
        Timeline = timeline;
        Events = events;
        PlanNodes = planNodes;
        Teardowns = QueryTeardown.GetTeardowns(timeline);

        timeline.SetCalls(callStore);

        PlanLink = IteratorPlanLinker.Link(planNodes, events);

        OperatorFrames = OperatorFrameMap.Build(callTree, events, PlanLink);
    }

    public IteratorPlanLink PlanLink { get; }

    public OperatorFrameMap OperatorFrames { get; }

    public AllocationIndex Allocation { get; init; } = AllocationIndex.Empty;

    public AddressMap AddressMap { get; init; } = AddressMap.Empty;

    public IReadOnlyDictionary<ulong, MemoryObjectInfo> MemoryObjectSnapshot { get; init; } = new Dictionary<ulong, MemoryObjectInfo>();

    public ObjectPoolSnapshot ObjectPool { get; init; } = ObjectPoolSnapshot.Empty;

    public TraceBufferPool BufferPool { get; init; } = TraceBufferPool.Empty;

    private CallTree CallTree { get; }

    private TraceCallStore CallStore { get; }

    private TraceTimeline Timeline { get; }

    private IReadOnlyList<EngineEvent> Events { get; }

    private IReadOnlyList<IteratorPlanNode> PlanNodes { get; }

    private IReadOnlyList<QueryTeardown> Teardowns { get; }

    private TraceOperatorLifetimeList? Lifetimes { get; set; }

    public TraceOperatorLifetimeList BuildLifetimes()
    {
        Lifetimes = IteratorLifetimeBuilder.Build(Timeline, Allocation, OperatorFrames.OperatorsByInstance, PlanNodes, Teardowns);

        return Lifetimes;
    }

    public TraceResult Build()
    {
        var lifetimes = Lifetimes ?? BuildLifetimes();

        var enclosing = EnclosingOperatorCallIndex.Build(CallStore, lifetimes.OperatorsByInstance);

        return new TraceResult(CallTree, CallStore, Timeline)
        {
            PlanLink = PlanLink,
            OperatorFrames = OperatorFrames,
            PlanNodes = PlanNodes,
            Teardowns = Teardowns,
            HoBtAccess = HoBtAccessMap.Build(CallStore, Timeline.MappedNodes),
            Allocation = Allocation,
            Lifetimes = lifetimes,
            EnclosingCalls = enclosing,
            AddressMap = AddressMap,
            MemoryObjects = MemoryObjectMap.Build(CallStore, Timeline.MappedNodes, MemoryObjectSnapshot),
            ObjectPool = ObjectPool,
            BufferPool = BufferPool,
            RowReturnMarks = TraceRowReturnMarkMap.Build(Timeline, CallStore, lifetimes, enclosing),
            Seeks = TraceSeekReader.Build(Timeline, CallStore, lifetimes, enclosing)
        };
    }
}
