using System.Buffers;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.Batches;

internal sealed class BatchHook(BatchFunctions functions, BatchIndex.Log log) : ITraceHook
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

    private List<ushort> Selection { get; } = [];

    private List<RawVector> Vectors { get; } = [];

    private List<ulong> Slots { get; } = [];

    private List<RawDeepValue> DeepValues { get; } = [];

    private ArrayBufferWriter<byte> Data { get; } = new();

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

        var capacity = replay.TryRead(batch + BatchCapacityOffset, out int value) && value is > 0 and <= MaximumBatchSelection
            ? (uint)value
            : 0;

        var raw = new RawBatch(call.Function,
                                         call.Instance,
                                         start.Iterator,
                                         batch,
                                         start.Consumer,
                                         call.CallCount,
                                         call.Call,
                                         0,
                                         0,
                                         0,
                                         0,
                                         0,
                                         capacity);

        if (!replay.TryRead(batch + BatchVectorsOffset, out ulong vectors)
            || !replay.TryRead(batch + BatchPureOffset, out ulong pure)
            || !replay.TryRead(batch + BatchVectorCountOffset, out int vectorCount)
            || !replay.TryRead(batch + BatchRowCountOffset, out int rowCount)
            || !replay.TryRead(batch + BatchSelectionOffset, out ulong selection)
            || rowCount < 0
            || rowCount > MaximumBatchSelection
            || vectorCount < 0
            || vectors < SmallestAddress)
        {
            Add(raw with { Flags = (uint)BatchCaptureFlags.Unreadable });

            return;
        }

        raw = ReadSelection(replay, selection, rowCount, raw);

        foreach (var vectorId in vectorIds)
        {
            Vectors.Add(ReadVector(replay, vectorId, vectors, pure, vectorCount));
        }

        Add(raw with { VectorCount = idCount });
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

    private RawBatch ReadSelection(ITraceReplay replay, ulong selection, int rowCount, RawBatch raw)
    {
        if (selection < SmallestAddress)
        {
            for (var index = 0; index < rowCount; index++)
            {
                Selection.Add((ushort)index);
            }

            return raw with { SelectedCount = (uint)rowCount, Flags = raw.Flags | (uint)BatchCaptureFlags.ImplicitSelection };
        }

        if (rowCount > 0)
        {
            CollectionsMarshal.SetCount(Selection, rowCount);

            if (replay.Read(selection, MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(Selection))) != rowCount * sizeof(ushort))
            {
                Selection.Clear();

                return raw with { Flags = raw.Flags | (uint)BatchCaptureFlags.UnreadableSelection };
            }
        }

        return raw with { SelectedCount = (uint)rowCount };
    }

    private RawVector ReadVector(ITraceReplay replay, ushort vectorId, ulong vectors, ulong pure, int vectorCount)
    {
        var firstSlot = Slots.Count;

        var firstDeepValue = DeepValues.Count;

        if (vectorId == 0 || vectorId > vectorCount)
        {
            return new RawVector((uint)firstSlot, 0, (uint)firstDeepValue, 0, vectorId, (ushort)BatchVectorFlags.Missing);
        }

        var index = (ulong)vectorId - 1;

        if (!replay.TryRead(vectors + index * BatchVectorEntrySize, out ulong buffer) || buffer < SmallestAddress)
        {
            return new RawVector((uint)firstSlot, 0, (uint)firstDeepValue, 0, vectorId, (ushort)BatchVectorFlags.Unreadable);
        }

        var flags = BatchVectorFlags.None;

        if (pure >= SmallestAddress && replay.TryRead(pure + index / 8, out byte pureBits) && ((pureBits >> (int)(index % 8)) & 1) != 0)
        {
            flags |= BatchVectorFlags.Pure;

            if (!replay.TryRead(buffer, out ulong slot))
            {
                flags |= BatchVectorFlags.Unreadable;

                return new RawVector((uint)firstSlot, 0, (uint)firstDeepValue, 0, vectorId, (ushort)flags);
            }

            Slots.Add(slot);
        }
        else
        {
            flags |= ReadSlots(replay, buffer);
        }

        var slotCount = Slots.Count - firstSlot;

        for (var slot = 0; slot < slotCount; slot++)
        {
            ReadDeepValue(replay, (uint)slot, Slots[firstSlot + slot]);
        }

        return new RawVector((uint)firstSlot,
                                  (uint)slotCount,
                                  (uint)firstDeepValue,
                                  (uint)(DeepValues.Count - firstDeepValue),
                                  vectorId,
                                  (ushort)flags);
    }

    private BatchVectorFlags ReadSlots(ITraceReplay replay, ulong buffer)
    {
        if (Selection.Count == 0)
        {
            return BatchVectorFlags.None;
        }

        var rowCount = 0;

        foreach (var row in Selection)
        {
            rowCount = Math.Max(rowCount, row + 1);
        }

        var first = Slots.Count;

        CollectionsMarshal.SetCount(Slots, first + rowCount);

        var block = CollectionsMarshal.AsSpan(Slots)[first..];

        var read = replay.Read(buffer, MemoryMarshal.AsBytes(block)) / sizeof(ulong);

        var flags = BatchVectorFlags.None;

        for (var row = read; row < rowCount; row++)
        {
            if (!replay.TryRead(buffer + (ulong)row * sizeof(ulong), out ulong slot))
            {
                flags |= BatchVectorFlags.UnreadableSlots;
            }

            block[row] = slot;
        }

        return flags;
    }

    private void ReadDeepValue(ITraceReplay replay, uint slot, ulong value)
    {
        if ((value & 1) == 0 || value <= LargestInlineTag)
        {
            return;
        }

        var pointer = value & ~1ul;

        var deepValue = new RawDeepValue(slot, (uint)Data.WrittenCount, 0, 0);

        if (pointer >= SmallestAddress && replay.TryRead(pointer - DeepLengthOffset, out ushort length))
        {
            var wanted = (int)Math.Min(length, MaximumValueBytes);

            var captured = wanted == 0 ? 0 : replay.Read(pointer, Data.GetSpan(wanted)[..wanted]);

            Data.Advance(captured);

            deepValue = deepValue with { Length = length, Captured = (uint)captured };
        }

        DeepValues.Add(deepValue);
    }

    private void Add(RawBatch raw)
    {
        log.Add([raw],
                CollectionsMarshal.AsSpan(Vectors),
                CollectionsMarshal.AsSpan(Selection),
                CollectionsMarshal.AsSpan(Slots),
                CollectionsMarshal.AsSpan(DeepValues),
                Data.WrittenSpan);

        Vectors.Clear();

        Selection.Clear();

        Slots.Clear();

        DeepValues.Clear();

        Data.ResetWrittenCount();
    }

    private readonly record struct BatchStart(ulong Iterator, ulong Data, ulong Consumer);
}
