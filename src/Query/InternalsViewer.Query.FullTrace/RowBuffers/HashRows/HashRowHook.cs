using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.HashRows.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

namespace InternalsViewer.Query.FullTrace.RowBuffers.HashRows;

internal sealed class HashRowHook(HashRowFunctions functions) : ITraceHook
{
    private const int IteratorArgument = 0;

    private const int PartitionGroupArgument = 0;

    private const int ContextArgument = 1;

    private const ulong LayoutPartitionGroupProbeOffset = 0x08;

    private const ulong LayoutProbeSlotOffset = 0x14;

    private const uint RowStatus = 0;

    private const int MaximumSlot = 64;

    private const TraceHookEvents EntryAndReturn = TraceHookEvents.Entry | TraceHookEvents.Return;

    public IEnumerable<TraceHookFunction> Functions => [.. functions.Functions.Keys.Select(f => new TraceHookFunction(f, EntryAndReturn))];

    public IReadOnlyList<RowBuffer> Rows => Captured;

    private HashSet<ulong> HashIterators { get; } = [.. functions.HashIterators];

    private Dictionary<uint, Stack<GetRowFrame>> Frames { get; } = [];

    private Dictionary<ulong, PartitionCall> PartitionCalls { get; } = [];

    private Dictionary<ulong, int> Slots { get; } = [];

    private Dictionary<ulong, byte[]> Records { get; } = [];

    private List<RowBuffer> Captured { get; } = [];

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!functions.Functions.TryGetValue(call.Function, out var type))
        {
            return;
        }

        if (type != HashRowFunctionType.GetRow)
        {
            PartitionCalls[call.Id] = new PartitionCall(call.Arguments[PartitionGroupArgument], call.Arguments[ContextArgument]);

            return;
        }

        var iterator = call.Arguments[IteratorArgument];

        if (replay.TryRead(iterator, out ulong vtable) && HashIterators.Contains(vtable))
        {
            GetFrames(call.Thread).Push(new GetRowFrame(call.Id, iterator, call.Arguments[ContextArgument]));
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (PartitionCalls.Remove(call.Id, out var partitionCall))
        {
            OnPartitionReturn(call, partitionCall, replay);

            return;
        }

        if (Pop(call.Thread, call.Id) is not { Slot: { } slot } frame
            || (uint)call.ReturnValue != RowStatus
            || GetRecordAddress(frame.Context, slot, replay) is not { } address
            || (Records.GetValueOrDefault(address) ?? RecordRowParser.Read(replay, address)) is not { } record)
        {
            return;
        }

        Captured.Add(new RowBuffer(call.Function,
                                   call.Instance,
                                   (int)call.Call,
                                   call.Position,
                                   frame.Iterator,
                                   address,
                                   RecordRowParser.Split(record, null),
                                   RowBufferFormat.HashTable)
        {
            Record = record
        });
    }

    public void OnAbandoned(in TraceHookCall call)
    {
        PartitionCalls.Remove(call.Id);

        Pop(call.Thread, call.Id);
    }

    private static ulong? GetRecordAddress(ulong context, int slot, ITraceReplay replay)
        => replay.TryRead(context + (ulong)slot * sizeof(ulong), out ulong address) && address >= ColumnValueParser.SmallestAddress
            ? address
            : null;

    private void OnPartitionReturn(in TraceHookCall call, PartitionCall partitionCall, ITraceReplay replay)
    {
        if (GetSlot(partitionCall.PartitionGroup, replay) is not { } slot)
        {
            return;
        }

        if (functions.Functions.TryGetValue(call.Function, out var type) && type == HashRowFunctionType.Insert)
        {
            if (GetRecordAddress(partitionCall.Context, slot, replay) is { } address
                && RecordRowParser.Read(replay, address) is { } record)
            {
                Records[address] = record;
            }

            return;
        }

        if (Frames.TryGetValue(call.Thread, out var frames)
            && frames.TryPeek(out var frame)
            && frame.Context == partitionCall.Context)
        {
            frames.Pop();

            frames.Push(frame with { Slot = slot });
        }
    }

    private int? GetSlot(ulong partitionGroup, ITraceReplay replay)
    {
        if (replay.TryRead(partitionGroup + LayoutPartitionGroupProbeOffset, out ulong probe)
            && replay.TryRead(probe + LayoutProbeSlotOffset, out int slot)
            && slot is >= 0 and < MaximumSlot)
        {
            Slots[partitionGroup] = slot;

            return slot;
        }

        return Slots.TryGetValue(partitionGroup, out var known) ? known : null;
    }

    private GetRowFrame? Pop(uint thread, ulong id)
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

    private Stack<GetRowFrame> GetFrames(uint thread)
    {
        if (!Frames.TryGetValue(thread, out var frames))
        {
            frames = [];

            Frames[thread] = frames;
        }

        return frames;
    }

    private readonly record struct GetRowFrame(ulong Id, ulong Iterator, ulong Context, int? Slot = null);

    private readonly record struct PartitionCall(ulong PartitionGroup, ulong Context);
}
