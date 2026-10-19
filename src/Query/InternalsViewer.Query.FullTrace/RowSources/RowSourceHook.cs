using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.RowSources.Enums;

namespace InternalsViewer.Query.FullTrace.RowSources;

internal sealed class RowSourceHook(RowSourceFunctions functions) : ITraceHook
{
    private const int IteratorArgument = 0;

    private const int ContextArgument = 1;

    private const int MaximumSlots = 64;

    private const int ObservedCalls = 4;

    private const int RowBytes = 64;

    private const ulong PlanObjectOffset = 0x10;

    private const ulong BatchPlanObjectOffset = 0x10;

    private const ulong PlanNodeIdOffset = 0x0C;

    private const ulong PlanQueryOffset = 0x28;

    private const TraceHookEvents EntryAndReturn = TraceHookEvents.Entry | TraceHookEvents.Return;

    public IEnumerable<TraceHookFunction> Functions => [.. functions.Functions.Keys.Select(f => new TraceHookFunction(f, EntryAndReturn))];

    private Dictionary<uint, Stack<RowSourceFrame>> Frames { get; } = [];

    private Dictionary<ulong, int> Observed { get; } = [];

    private Dictionary<(ulong Context, int Slot), ulong> Owners { get; } = [];

    private Dictionary<ulong, ulong> Creators { get; } = [];

    private Dictionary<ulong, IteratorPlanNode> PlanNodes { get; } = [];

    private Dictionary<ulong, ulong> PendingPlanNodes { get; } = [];

    private Dictionary<ulong, PendingFactory> Factories { get; } = [];

    private Dictionary<ulong, TracePoint> Constructions { get; } = [];

    private HashSet<ulong> TearingDown { get; } = [];

    private List<QueryTeardown> Teardowns { get; } = [];

    private List<ulong> ResolvedPlanNodes { get; } = [];

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!functions.Functions.TryGetValue(call.Function, out var type))
        {
            return;
        }

        var iterator = call.Arguments[IteratorArgument];

        if (type != RowSourceFunctionType.Iterator)
        {
            ResolvePlanNodes(replay);
        }

        switch (type)
        {
            case RowSourceFunctionType.BatchFactory:
                Factories[call.Id] = new PendingFactory(iterator, new TracePoint(call.Position, call.Instructions));
                return;

            case RowSourceFunctionType.Teardown:
                TearingDown.Add(call.Id);
                return;

            case RowSourceFunctionType.Constructor:
                Constructions[iterator] = new TracePoint(call.Position, call.Instructions);
                break;

            case RowSourceFunctionType.BatchIterator:
                AddPlanNode(replay, iterator);
                return;

            case RowSourceFunctionType.Iterator:
                AddPlanNode(replay, iterator);
                break;
        }

        var context = call.Arguments[ContextArgument];

        var isObserved = type == RowSourceFunctionType.Iterator
                         && context >= TraceAddress.Smallest
                         && Observed.GetValueOrDefault(iterator) < ObservedCalls;

        GetFrames(call.Thread).Push(new RowSourceFrame(call.Id, type, iterator, context, isObserved ? ReadSlots(replay, context) : null));
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (TearingDown.Remove(call.Id))
        {
            Teardowns.Add(new QueryTeardown(call.Thread, new TracePoint(call.Position, call.Instructions)));

            return;
        }

        if (Factories.Remove(call.Id, out var factory))
        {
            if (call.ReturnValue >= TraceAddress.Smallest && !PlanNodes.ContainsKey(call.ReturnValue))
            {
                PendingPlanNodes[call.ReturnValue] = factory.PlanObject;

                Constructions[call.ReturnValue] = factory.Started;

                ResolvePlanNodes(replay);
            }

            return;
        }

        if (Pop(call.Thread, call.Id) is not { } frame)
        {
            return;
        }

        if (frame.Type == RowSourceFunctionType.Creator && call.ReturnValue >= TraceAddress.Smallest)
        {
            Creators.TryAdd(call.ReturnValue, frame.Iterator);
        }

        if (frame.Slots is not { } before)
        {
            return;
        }

        Observed[frame.Iterator] = Observed.GetValueOrDefault(frame.Iterator) + 1;

        var after = ReadSlots(replay, frame.Context);

        for (var slot = 0; slot < MaximumSlots; slot++)
        {
            if (IsChanged(before[slot], after[slot]))
            {
                Owners.TryAdd((frame.Context, slot), frame.Iterator);
            }
        }
    }

    public void OnAbandoned(in TraceHookCall call)
    {
        Factories.Remove(call.Id);

        TearingDown.Remove(call.Id);

        Pop(call.Thread, call.Id);
    }

    public ulong GetOwner(ulong context, int slot) => Owners.GetValueOrDefault((context, slot));

    public ulong GetCreator(ulong value) => Creators.GetValueOrDefault(value);

    public IReadOnlyList<IteratorPlanNode> GetPlanNodes() => [.. PlanNodes.Values];

    public IReadOnlyList<QueryTeardown> GetTeardowns() => Teardowns;

    public ulong GetCurrentIterator(uint thread)
        => Frames.TryGetValue(thread, out var frames) && frames.TryPeek(out var frame) ? frame.Iterator : 0;

    private static bool IsChanged(SlotState? before, SlotState? after)
    {
        if (before is not { } from || after is not { Handle: >= TraceAddress.Smallest } to)
        {
            return false;
        }

        if (from.Handle != to.Handle)
        {
            return true;
        }

        return from.Row is { } fromRow && to.Row is { } toRow && !fromRow.AsSpan().SequenceEqual(toRow);
    }

    private static SlotState?[] ReadSlots(ITraceReplay replay, ulong context)
    {
        var slots = new SlotState?[MaximumSlots];

        for (var slot = 0; slot < MaximumSlots; slot++)
        {
            if (replay.TryRead(context + (ulong)slot * sizeof(ulong), out ulong handle))
            {
                slots[slot] = new SlotState(handle, ReadRow(replay, handle));
            }
        }

        return slots;
    }

    private static byte[]? ReadRow(ITraceReplay replay, ulong handle)
    {
        if (handle < TraceAddress.Smallest)
        {
            return null;
        }

        var row = new byte[RowBytes];

        return replay.Read(handle, row) == row.Length ? row : null;
    }

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
            || query < TraceAddress.Smallest)
        {
            return null;
        }

        return new IteratorPlanNode(iterator,
                                    planObject,
                                    batchPlanObject,
                                    query,
                                    nodeId,
                                    Constructions.TryGetValue(iterator, out var constructed) ? constructed : null);
    }

    private PlanObjectClass? GetPlanObjectClass(ITraceReplay replay, ulong planObject)
        => planObject >= TraceAddress.Smallest
           && replay.TryRead(planObject, out ulong vtable)
           && functions.PlanObjects.TryGetValue(vtable, out var planClass)
            ? planClass
            : null;

    private RowSourceFrame? Pop(uint thread, ulong id)
    {
        if (!Frames.TryGetValue(thread, out var frames) || frames.All(f => f.Id != id))
        {
            return null;
        }

        while (frames.TryPop(out var frame))
        {
            if (frame.Id == id)
            {
                return frame;
            }
        }

        return null;
    }

    private Stack<RowSourceFrame> GetFrames(uint thread)
    {
        if (!Frames.TryGetValue(thread, out var frames))
        {
            frames = [];

            Frames[thread] = frames;
        }

        return frames;
    }

    private readonly record struct SlotState(ulong Handle, byte[]? Row);

    private readonly record struct PendingFactory(ulong PlanObject, TracePoint Started);

    private sealed record RowSourceFrame(ulong Id, RowSourceFunctionType Type, ulong Iterator, ulong Context, SlotState?[]? Slots);
}
