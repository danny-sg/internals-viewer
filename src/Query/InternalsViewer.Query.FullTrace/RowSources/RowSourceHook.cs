using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.RowBuffers.Parsers;
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

    private Dictionary<ulong, List<(ulong Context, int Slot)>> OwnedSlots { get; } = [];

    private Dictionary<ulong, ulong> Creators { get; } = [];

    private Dictionary<ulong, IteratorPlanNode> PlanNodes { get; } = [];

    private Dictionary<ulong, ulong> PendingPlanNodes { get; } = [];

    private Dictionary<ulong, ulong> Factories { get; } = [];

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
                Factories[call.Id] = iterator;
                return;

            case RowSourceFunctionType.BatchIterator:
                AddPlanNode(replay, iterator);
                return;

            case RowSourceFunctionType.Iterator:
                AddPlanNode(replay, iterator);
                break;
        }

        var context = call.Arguments[ContextArgument];

        var isObserved = type == RowSourceFunctionType.Iterator
                         && context >= ColumnValueParser.SmallestAddress
                         && Observed.GetValueOrDefault(iterator) < ObservedCalls;

        GetFrames(call.Thread).Push(new RowSourceFrame(call.Id, type, iterator, context, isObserved ? ReadSlots(replay, context) : null));
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (Factories.Remove(call.Id, out var planObject))
        {
            if (call.ReturnValue >= ColumnValueParser.SmallestAddress && !PlanNodes.ContainsKey(call.ReturnValue))
            {
                PendingPlanNodes[call.ReturnValue] = planObject;

                ResolvePlanNodes(replay);
            }

            return;
        }

        if (Pop(call.Thread, call.Id) is not { } frame)
        {
            return;
        }

        if (frame.Type == RowSourceFunctionType.Creator && call.ReturnValue >= ColumnValueParser.SmallestAddress)
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
                AddOwner(frame.Context, slot, frame.Iterator);
            }
        }
    }

    public void OnAbandoned(in TraceHookCall call)
    {
        Factories.Remove(call.Id);

        Pop(call.Thread, call.Id);
    }

    public ulong GetOwner(ulong context, int slot) => Owners.GetValueOrDefault((context, slot));

    public IReadOnlyList<(ulong Context, int Slot)> GetSlots(ulong iterator) => OwnedSlots.TryGetValue(iterator, out var slots) ? slots : [];

    public ulong GetCreator(ulong value) => Creators.GetValueOrDefault(value);

    public IReadOnlyList<IteratorPlanNode> GetPlanNodes() => [.. PlanNodes.Values];

    public ulong GetCurrentIterator(uint thread)
        => Frames.TryGetValue(thread, out var frames) && frames.TryPeek(out var frame) ? frame.Iterator : 0;

    private static bool IsChanged(SlotState? before, SlotState? after)
    {
        if (before is not { } from || after is not { Handle: >= ColumnValueParser.SmallestAddress } to)
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
        if (handle < ColumnValueParser.SmallestAddress)
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
            || query < ColumnValueParser.SmallestAddress)
        {
            return null;
        }

        return new IteratorPlanNode(iterator, planObject, batchPlanObject, query, nodeId);
    }

    private PlanObjectClass? GetPlanObjectClass(ITraceReplay replay, ulong planObject)
        => planObject >= ColumnValueParser.SmallestAddress
           && replay.TryRead(planObject, out ulong vtable)
           && functions.PlanObjects.TryGetValue(vtable, out var planClass)
            ? planClass
            : null;

    private void AddOwner(ulong context, int slot, ulong iterator)
    {
        if (!Owners.TryAdd((context, slot), iterator))
        {
            return;
        }

        if (!OwnedSlots.TryGetValue(iterator, out var slots))
        {
            slots = [];

            OwnedSlots[iterator] = slots;
        }

        slots.Add((context, slot));
    }

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

    private sealed record RowSourceFrame(ulong Id, RowSourceFunctionType Type, ulong Iterator, ulong Context, SlotState?[]? Slots);
}
