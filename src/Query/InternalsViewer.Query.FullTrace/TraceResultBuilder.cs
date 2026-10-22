using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.CustomHooks;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.FullTrace.Timeline;
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

    public MemoryIndex Memory { get; init; } = MemoryIndex.Empty;

    public MemoryMap MemoryMap { get; init; } = MemoryMap.Empty;

    public ObjectPoolSnapshot ObjectPool { get; init; } = ObjectPoolSnapshot.Empty;

    public CustomHookStore CustomHooks { get; init; } = CustomHookStore.Empty;

    private CallStackTree CallStack { get; }

    private TraceCallStore CallStore { get; }

    private TraceTimeline Timeline { get; }

    private IReadOnlyList<EngineEvent> Events { get; }

    private IReadOnlyList<IteratorPlanNode> PlanNodes { get; }

    private IReadOnlyList<QueryTeardown> Teardowns { get; }

    private TraceOperatorLifetimes? Lifetimes { get; set; }

    public TraceOperatorLifetimes BuildLifetimes()
    {
        Lifetimes = IteratorLifetimeBuilder.Build(Timeline, Memory, [.. Events.OfType<ExecutionOperatorEvent>()], PlanNodes, Teardowns);

        return Lifetimes;
    }

    public TraceResult Build(IReadOnlyList<TraceOutputStructure> outputStructures)
    {
        var lifetimes = Lifetimes ?? BuildLifetimes();

        return new TraceResult(CallStack, CallStore, Timeline)
        {
            PlanLink = PlanLink,
            PlanNodes = PlanNodes,
            Teardowns = Teardowns,
            HoBtAccess = HoBtAccessMap.Build(CallStore, Timeline.MappedNodes),
            Memory = Memory,
            Lifetimes = lifetimes,
            MemoryMap = MemoryMap,
            ObjectPool = ObjectPool,
            CustomHooks = CustomHooks,
            OutputStructures = outputStructures,
            RowReturnMarks = TraceRowReturnMarks.Build(Timeline, CallStore, lifetimes),
            Reads = TraceReads.Build(Timeline, CallStore, lifetimes),
            Evaluations = TraceEvaluations.Build(Timeline, CallStore, lifetimes)
        };
    }
}
