using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;
using InternalsViewer.Query.Extensions;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

internal sealed class PlanNodeHook(AddressTable<PlanNodeFunctionType> functions,
                                   AddressTable<PlanObjectClass> planObjects,
                                   PlanNodeLayout layout) : ITraceHook
{
    private const int IteratorArgument = 0;

    private const TraceHookEvents EntryAndReturn = TraceHookEvents.Entry | TraceHookEvents.Return;

    public IEnumerable<TraceHookFunction> Functions
        => [.. functions.Select(f => new TraceHookFunction(f.Key, HasReturn(f.Value) ? EntryAndReturn : TraceHookEvents.Entry))];

    private Dictionary<ulong, IteratorPlanNode> PlanNodes { get; } = [];

    private Dictionary<ulong, ulong> PendingPlanNodes { get; } = [];

    private Dictionary<ulong, IteratorPlanNode> ResolvedPlanObjects { get; } = [];

    private Dictionary<ulong, PendingFactory> Factories { get; } = [];

    private Dictionary<ulong, TracePoint> Constructions { get; } = [];

    private HashSet<ulong> BatchHelpers { get; } = [];

    private List<ulong> ResolvedPlanNodes { get; } = [];

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!functions.TryGetValue(call.FunctionAddress, out var type))
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
                Factories[call.CallId] = new PendingFactory(iterator, new TracePoint(call.Position, call.Instructions));
                return;

            case PlanNodeFunctionType.Constructor:
                Constructions[iterator] = new TracePoint(call.Position, call.Instructions);
                return;

            case PlanNodeFunctionType.BatchHelper:
                BatchHelpers.Add(iterator);
                AddPlanNode(replay, iterator, layout.RowIteratorPlanObjectOffset);
                return;

            case PlanNodeFunctionType.Iterator:
                AddPlanNode(replay, iterator, layout.RowIteratorPlanObjectOffset);
                return;

            case PlanNodeFunctionType.BatchIterator:
                AddPlanNode(replay, iterator, layout.BatchIteratorPlanObjectOffset);
                return;
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (Factories.Remove(call.CallId, out var factory)
            && call.ReturnValue >= TraceAddress.MinimumAddress
            && !PlanNodes.ContainsKey(call.ReturnValue))
        {
            PendingPlanNodes[call.ReturnValue] = factory.PlanObject;

            Constructions[call.ReturnValue] = factory.Started;

            ResolvePlanNodes(replay);
        }
    }

    public void OnAbandoned(in TraceHookCall call) => Factories.Remove(call.CallId);

    public IReadOnlyList<IteratorPlanNode> GetPlanNodes() => [.. PlanNodes.Values];

    private static bool HasReturn(PlanNodeFunctionType type) => type is PlanNodeFunctionType.BatchFactory;

    private void AddPlanNode(ITraceReplay replay, ulong iterator, ulong planObjectOffset)
    {
        if (PlanNodes.ContainsKey(iterator)
            || PendingPlanNodes.ContainsKey(iterator)
            || !replay.TryRead(iterator + planObjectOffset, out ulong planObject))
        {
            return;
        }

        if (ResolvedPlanObjects.TryGetValue(planObject, out var resolved))
        {
            PlanNodes[iterator] = resolved with
            {
                Iterator = iterator,
                Constructed = Constructions.TryGetValue(iterator, out var constructed) ? constructed : null,
                IsBatchHelper = BatchHelpers.Contains(iterator)
            };

            return;
        }

        if (GetPlanObjectClass(replay, planObject) is null)
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

                ResolvedPlanObjects.TryAdd(planObject, planNode);

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

            if (!replay.TryRead(batchPlanObject + layout.BatchPlanObjectOffset, out planObject))
            {
                return null;
            }
        }

        if (GetPlanObjectClass(replay, planObject) != PlanObjectClass.Row
            || !replay.TryRead(planObject + layout.PlanNodeIdOffset, out int nodeId)
            || !replay.TryRead(planObject + layout.PlanQueryOffset, out ulong query)
            || nodeId < 0
            || query < TraceAddress.MinimumAddress)
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
        => planObject >= TraceAddress.MinimumAddress
           && replay.TryRead(planObject, out ulong vtable)
           && planObjects.TryGetValue(vtable, out var planClass)
            ? planClass
            : null;

    private readonly record struct PendingFactory(ulong PlanObject, TracePoint Started);
}
