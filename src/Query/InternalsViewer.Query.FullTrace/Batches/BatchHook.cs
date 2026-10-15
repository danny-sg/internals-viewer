using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Batches.Enums;
using InternalsViewer.Query.FullTrace.Hooks.Enums;

namespace InternalsViewer.Query.FullTrace.Batches;

internal sealed class BatchHook(BatchFunctions functions) : ITraceHook
{
    private const ulong SmallestAddress = 0x10000;

    private const int IteratorArgument = 0;

    private const int DataArgument = 1;

    private const ulong BatchIdsOffset = 0x08;

    private const ulong BatchVectorsOffset = 0x08;

    private const ulong BatchPureOffset = 0x10;

    private const ulong BatchVectorCountOffset = 0x1C;

    private const ulong BatchCapacityOffset = 0x24;

    private const ulong BatchRowCountOffset = 0x28;

    private const ulong BatchSelectionOffset = 0x38;

    private const ulong BatchVectorEntrySize = 0x58;

    private const int MaximumBatchVectors = 1024;

    private const int MaximumBatchSelection = 0x10000;

    private const ulong LargestInlineTag = 0xF;

    private const ulong DeepLengthOffset = 2;

    private const ulong MaximumValueBytes = 8000;

    public IEnumerable<TraceHookFunction> Functions
        => functions.Functions.Select(f => new TraceHookFunction(f, TraceHookEvents.Entry | TraceHookEvents.Return));

    private Dictionary<ulong, BatchStart> Starts { get; } = [];

    private List<BatchCapture> Batches { get; } = [];

    public BatchIndex Build() => new(Batches);

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        var data = call.Arguments[DataArgument];

        if (data < SmallestAddress)
        {
            return;
        }

        var iterator = call.Arguments[IteratorArgument];

        Starts[call.Id] = new BatchStart(iterator, data, GetConsumer(replay.GetFrames(call.Thread), iterator));
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Starts.Remove(call.Id, out var start))
        {
            return;
        }

        if (!replay.TryRead(start.Data, out ulong batch)
            || !replay.TryRead(start.Data + BatchIdsOffset, out ulong ids)
            || batch < SmallestAddress
            || ids < SmallestAddress
            || !replay.TryRead(ids, out ushort idCount)
            || idCount > MaximumBatchVectors)
        {
            return;
        }

        Span<ushort> vectorIds = stackalloc ushort[idCount];

        if (idCount > 0 && replay.Read(ids + sizeof(ushort), MemoryMarshal.AsBytes(vectorIds)) != idCount * sizeof(ushort))
        {
            return;
        }

        var capacity = replay.TryRead(batch + BatchCapacityOffset, out int value) && value is > 0 and <= MaximumBatchSelection ? value : 0;

        var flags = BatchCaptureFlags.Unreadable;

        ushort[] selection = [];

        BatchCaptureVector[] vectors = [];

        if (replay.TryRead(batch + BatchVectorsOffset, out ulong vectorTable)
            && replay.TryRead(batch + BatchPureOffset, out ulong pure)
            && replay.TryRead(batch + BatchVectorCountOffset, out int vectorCount)
            && replay.TryRead(batch + BatchRowCountOffset, out int rowCount)
            && replay.TryRead(batch + BatchSelectionOffset, out ulong selected)
            && rowCount is >= 0 and <= MaximumBatchSelection
            && vectorCount >= 0
            && vectorTable >= SmallestAddress)
        {
            (selection, flags) = ReadSelection(replay, selected, rowCount);

            var slotCount = selection.Length == 0 ? 0 : selection.Max() + 1;

            vectors = new BatchCaptureVector[vectorIds.Length];

            for (var index = 0; index < vectors.Length; index++)
            {
                vectors[index] = ReadVector(replay, vectorIds[index], vectorTable, pure, vectorCount, slotCount);
            }
        }

        Batches.Add(new BatchCapture(call.Function,
                                     call.Instance,
                                     (int)call.Call,
                                     call.CallCount,
                                     start.Iterator,
                                     batch,
                                     start.Consumer,
                                     flags,
                                     selection,
                                     vectors,
                                     capacity));
    }

    public void OnAbandoned(in TraceHookCall call) => Starts.Remove(call.Id);

    private static ulong GetConsumer(ReadOnlySpan<TraceHookFrame> frames, ulong iterator)
    {
        foreach (var frame in frames)
        {
            if (frame.Node >= 0 && frame.Instance != 0 && frame.Instance != iterator)
            {
                return frame.Instance;
            }
        }

        return 0;
    }

    private static (ushort[] Selection, BatchCaptureFlags Flags) ReadSelection(ITraceReplay replay, ulong address, int rowCount)
    {
        if (address < SmallestAddress)
        {
            return ([.. Enumerable.Range(0, rowCount).Select(i => (ushort)i)], BatchCaptureFlags.ImplicitSelection);
        }

        var selection = new ushort[rowCount];

        return replay.Read(address, MemoryMarshal.AsBytes(selection.AsSpan())) == rowCount * sizeof(ushort)
            ? (selection, BatchCaptureFlags.None)
            : ([], BatchCaptureFlags.UnreadableSelection);
    }

    private static BatchCaptureVector ReadVector(ITraceReplay replay,
                                                 ushort vectorId,
                                                 ulong vectors,
                                                 ulong pure,
                                                 int vectorCount,
                                                 int slotCount)
    {
        if (vectorId == 0 || vectorId > vectorCount)
        {
            return new BatchCaptureVector(vectorId, BatchVectorFlags.Missing, [], new Dictionary<int, BatchDeepValue>());
        }

        var index = (ulong)vectorId - 1;

        if (!replay.TryRead(vectors + index * BatchVectorEntrySize, out ulong buffer) || buffer < SmallestAddress)
        {
            return new BatchCaptureVector(vectorId, BatchVectorFlags.Unreadable, [], new Dictionary<int, BatchDeepValue>());
        }

        var flags = BatchVectorFlags.None;

        ulong[] slots;

        if (pure >= SmallestAddress && replay.TryRead(pure + index / 8, out byte pureBits) && ((pureBits >> (int)(index % 8)) & 1) != 0)
        {
            if (!replay.TryRead(buffer, out ulong slot))
            {
                return new BatchCaptureVector(vectorId,
                                              BatchVectorFlags.Pure | BatchVectorFlags.Unreadable,
                                              [],
                                              new Dictionary<int, BatchDeepValue>());
            }

            flags = BatchVectorFlags.Pure;

            slots = [slot];
        }
        else
        {
            slots = ReadSlots(replay, buffer, slotCount, ref flags);
        }

        return new BatchCaptureVector(vectorId, flags, slots, ReadDeepValues(replay, slots));
    }

    private static ulong[] ReadSlots(ITraceReplay replay, ulong buffer, int slotCount, ref BatchVectorFlags flags)
    {
        var slots = new ulong[slotCount];

        var read = replay.Read(buffer, MemoryMarshal.AsBytes(slots.AsSpan())) / sizeof(ulong);

        for (var row = read; row < slots.Length; row++)
        {
            if (!replay.TryRead(buffer + (ulong)row * sizeof(ulong), out ulong slot))
            {
                flags |= BatchVectorFlags.UnreadableSlots;
            }

            slots[row] = slot;
        }

        return slots;
    }

    private static Dictionary<int, BatchDeepValue> ReadDeepValues(ITraceReplay replay, ulong[] slots)
    {
        var deepValues = new Dictionary<int, BatchDeepValue>();

        for (var slot = 0; slot < slots.Length; slot++)
        {
            var value = slots[slot];

            if ((value & 1) == 0 || value <= LargestInlineTag)
            {
                continue;
            }

            var pointer = value & ~1ul;

            if (pointer < SmallestAddress || !replay.TryRead(pointer - DeepLengthOffset, out ushort length))
            {
                deepValues[slot] = new BatchDeepValue(0, []);

                continue;
            }

            var data = new byte[(int)Math.Min(length, MaximumValueBytes)];

            var captured = data.Length == 0 ? 0 : replay.Read(pointer, data);

            deepValues[slot] = new BatchDeepValue(length, captured == data.Length ? data : data[..captured]);
        }

        return deepValues;
    }

    private readonly record struct BatchStart(ulong Iterator, ulong Data, ulong Consumer);
}
