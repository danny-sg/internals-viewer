using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;
using InternalsViewer.Query.Extensions;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

internal sealed class PlanNodeHook(PlanNodeFunctions functions) : ITraceHook
{
    private const int IteratorArgument = 0;

    private const ulong PlanObjectOffset = 0x10;

    private const ulong BatchPlanObjectOffset = 0x10;

    private const ulong PlanNodeIdOffset = 0x0C;

    private const ulong PlanQueryOffset = 0x28;

    private const TraceHookEvents EntryAndReturn = TraceHookEvents.Entry | TraceHookEvents.Return;

    public IEnumerable<TraceHookFunction> Functions
        => [.. functions.Functions.Select(f => new TraceHookFunction(f.Key, HasReturn(f.Value) ? EntryAndReturn : TraceHookEvents.Entry))];

    private Dictionary<ulong, IteratorPlanNode> PlanNodes { get; } = [];

    private Dictionary<ulong, ulong> PendingPlanNodes { get; } = [];

    private Dictionary<ulong, PendingFactory> Factories { get; } = [];

    private Dictionary<ulong, TracePoint> Constructions { get; } = [];

    private HashSet<ulong> TearingDown { get; } = [];

    private HashSet<ulong> BatchHelpers { get; } = [];

    private List<QueryTeardown> Teardowns { get; } = [];

    private List<ulong> ResolvedPlanNodes { get; } = [];

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!functions.Functions.TryGetValue(call.Function, out var type))
        {
            return;
        }

        var iterator = call.Arguments[IteratorArgument];

        if (type is not (PlanNodeFunctionType.Iterator or PlanNodeFunctionType.BatchHelper))
        {
            ResolvePlanNodes(replay);
        }

        switch (type)
        {
            case PlanNodeFunctionType.BatchFactory:
                Factories[call.Id] = new PendingFactory(iterator, new TracePoint(call.Position, call.Instructions));
                return;

            case PlanNodeFunctionType.Teardown:
                TearingDown.Add(call.Id);
                return;

            case PlanNodeFunctionType.Constructor:
                Constructions[iterator] = new TracePoint(call.Position, call.Instructions);
                return;

            case PlanNodeFunctionType.BatchHelper:
                BatchHelpers.Add(iterator);
                AddPlanNode(replay, iterator);
                return;

            case PlanNodeFunctionType.Iterator or PlanNodeFunctionType.BatchIterator:
                AddPlanNode(replay, iterator);
                return;
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (TearingDown.Remove(call.Id))
        {
            Teardowns.Add(new QueryTeardown(call.Thread, new TracePoint(call.Position, call.Instructions)));

            return;
        }

        if (Factories.Remove(call.Id, out var factory)
            && call.ReturnValue >= TraceAddress.MinAddress
            && !PlanNodes.ContainsKey(call.ReturnValue))
        {
            PendingPlanNodes[call.ReturnValue] = factory.PlanObject;

            Constructions[call.ReturnValue] = factory.Started;

            ResolvePlanNodes(replay);
        }
    }

    public void OnAbandoned(in TraceHookCall call)
    {
        Factories.Remove(call.Id);

        TearingDown.Remove(call.Id);
    }

    public IReadOnlyList<IteratorPlanNode> GetPlanNodes() => [.. PlanNodes.Values];

    public IReadOnlyList<QueryTeardown> GetTeardowns() => Teardowns;

    private static bool HasReturn(PlanNodeFunctionType type) => type is PlanNodeFunctionType.BatchFactory or PlanNodeFunctionType.Teardown;

    private void AddPlanNode(ITraceReplay replay, ulong iterator)
    {
        if (PlanNodes.ContainsKey(iterator)
            || PendingPlanNodes.ContainsKey(iterator)
            || !replay.TryRead(iterator + PlanObjectOffset, out ulong planObject)
            || GetPlanObjectClass(replay, planObject) is null)
        {
            return;
        }

        PendingPlanNodes[iterator] = planObject;

        ResolvePlanNodes(replay);
    }

    private void ResolvePlanNodes(ITraceReplay replay)
    {
        foreach (var (iterator, planObject) in PendingPlanNodes)
        {
            if (GetPlanNode(replay, iterator, planObject) is { } planNode)
            {
                PlanNodes[iterator] = planNode;

                ResolvedPlanNodes.Add(iterator);
            }
        }

        foreach (var iterator in ResolvedPlanNodes)
        {
            PendingPlanNodes.Remove(iterator);
        }

        ResolvedPlanNodes.Clear();
    }

    private IteratorPlanNode? GetPlanNode(ITraceReplay replay, ulong iterator, ulong planObject)
    {
        var batchPlanObject = 0UL;

        if (GetPlanObjectClass(replay, planObject) == PlanObjectClass.Batch)
        {
            batchPlanObject = planObject;

            if (!replay.TryRead(batchPlanObject + BatchPlanObjectOffset, out planObject))
            {
                return null;
            }
        }

        if (GetPlanObjectClass(replay, planObject) != PlanObjectClass.Row
            || !replay.TryRead(planObject + PlanNodeIdOffset, out int nodeId)
            || !replay.TryRead(planObject + PlanQueryOffset, out ulong query)
            || nodeId < 0
            || query < TraceAddress.MinAddress)
        {
            return null;
        }

        return new IteratorPlanNode(iterator,
                                    planObject,
                                    batchPlanObject,
                                    query,
                                    nodeId,
                                    Constructions.TryGetValue(iterator, out var constructed) ? constructed : null,
                                    BatchHelpers.Contains(iterator));
    }

    private PlanObjectClass? GetPlanObjectClass(ITraceReplay replay, ulong planObject)
        => planObject >= TraceAddress.MinAddress
           && replay.TryRead(planObject, out ulong vtable)
           && functions.PlanObjects.TryGetValue(vtable, out var planClass)
            ? planClass
            : null;

    private readonly record struct PendingFactory(ulong PlanObject, TracePoint Started);
}
