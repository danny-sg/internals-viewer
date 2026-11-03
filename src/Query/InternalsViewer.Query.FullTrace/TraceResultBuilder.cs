using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.MemoryObject;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.Memory;

namespace InternalsViewer.Query.FullTrace;

internal sealed class TraceResultBuilder
{
    public TraceResultBuilder(CallStackTree callStack,
                              TraceCallStore callStore,
                              TraceTimeline timeline,
                              IReadOnlyList<EngineEvent> events,
                              IReadOnlyList<IteratorPlanNode> planNodes,
                              IReadOnlyList<QueryTeardown> teardowns)
    {
        CallStack = callStack;
        CallStore = callStore;
        Timeline = timeline;
        Events = events;
        PlanNodes = planNodes;
        Teardowns = teardowns;

        timeline.SetCalls(callStore);

        PlanLink = IteratorPlanLinker.Link(planNodes, events);
    }

    public IteratorPlanLink PlanLink { get; }

    public AllocationIndex Allocation { get; init; } = AllocationIndex.Empty;

    public AddressMap AddressMap { get; init; } = AddressMap.Empty;

    public IReadOnlyDictionary<ulong, MemoryObjectInfo> MemoryObjectSnapshot { get; init; } = new Dictionary<ulong, MemoryObjectInfo>();

    public ObjectPoolSnapshot ObjectPool { get; init; } = ObjectPoolSnapshot.Empty;

    public TraceBufferPool BufferPool { get; init; } = TraceBufferPool.Empty;

    private CallStackTree CallStack { get; }

    private TraceCallStore CallStore { get; }

    private TraceTimeline Timeline { get; }

    private IReadOnlyList<EngineEvent> Events { get; }

    private IReadOnlyList<IteratorPlanNode> PlanNodes { get; }

    private IReadOnlyList<QueryTeardown> Teardowns { get; }

    private TraceOperatorLifetimeList? Lifetimes { get; set; }

    public TraceOperatorLifetimeList BuildLifetimes()
    {
        Lifetimes = IteratorLifetimeBuilder.Build(Timeline, Allocation, [.. Events.OfType<ExecutionOperatorEvent>()], PlanNodes, Teardowns);

        return Lifetimes;
    }

    public TraceResult Build(IReadOnlyList<TraceOutputStructure> outputStructures)
    {
        var lifetimes = Lifetimes ?? BuildLifetimes();

        var enclosing = EnclosingOperatorCallIndex.Build(CallStore, lifetimes.OperatorsByInstance);

        return new TraceResult(CallStack, CallStore, Timeline)
        {
            PlanLink = PlanLink,
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
            OutputStructures = outputStructures,
            RowReturnMarks = TraceRowReturnMarkMap.Build(Timeline, CallStore, lifetimes, enclosing),
            Reads = TraceReadResolver.Build(Timeline, CallStore, lifetimes, enclosing),
            Evaluations = TraceEvaluationReader.Build(Timeline, CallStore, lifetimes, enclosing)
        };
    }
}
