using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.RowBuffers;

internal sealed class RowBufferHook(RowBufferFunctions functions, RowBufferIndex.Log log) : ITraceHook
{
    private const ulong SmallestAddress = 0x10000;

    private const int ArgumentRegisters = 4;

    private const ulong BindingCountOffset = 0x62;

    private const ulong BindingArrayOffset = 0x70;

    private const ulong BindingSize = 0x50;

    private const ulong BindingRecordOffset = 0x00;

    private const ulong BindingValueOffset = 0x28;

    private const ulong BindingStatusOffset = 0x30;

    private const ulong BindingFixedLengthOffset = 0x3C;

    private const ulong BindingFlagsOffset = 0x40;

    private const ulong BindingInfoOffset = 0x48;

    private const ulong BindingInfoLengthOffset = 0x18;

    private const ulong BindingInfoFlagsOffset = 0x2A;

    private const uint ByReferenceBinding = 0x800;

    private const uint LengthBinding = 0x100;

    private const byte AlternateBufferBinding = 0x10;

    private const int MaximumBindings = 1024;

    private const ulong RowValueSize = 0x20;

    private const ulong RowValueStatusOffset = 0x00;

    private const ulong RowValueDataOffset = 0x08;

    private const ulong RowValueLengthOffset = 0x10;

    private const ulong MaximumRowBufferBytes = 0x10000;

    private const ulong MaximumValueBytes = 8000;

    private const ulong DataPageSize = 0x2000;

    private const ulong PageHeaderSize = 96;

    private const ulong PageIdOffset = 0x20;

    private const int PageIdSize = 6;

    private const int PageIdFileOffset = 4;

    private const ulong LsnOffset = 0x28;

    private const int LsnSize = 10;

    private const byte PageHeaderVersion = 1;

    public IEnumerable<TraceHookFunction> Functions
        => Registers.Keys.Select(f => new TraceHookFunction(f, TraceHookEvents.Entry | TraceHookEvents.Return));

    private Dictionary<ulong, int> Registers { get; } = functions.Reads
                                                                 .Where(r => r.Register is >= 0 and < ArgumentRegisters)
                                                                 .GroupBy(r => r.Function)
                                                                 .ToDictionary(g => g.Key, g => g.Last().Register);

    private Dictionary<ulong, RowBufferLayout> Layouts { get; } = functions.Layouts
                                                                           .GroupBy(l => l.VTable)
                                                                           .ToDictionary(g => g.Key, g => g.Last());

    private Dictionary<ulong, RowBufferStart> Starts { get; } = [];

    private Dictionary<ulong, BindingSet> BindingSets { get; } = [];

    private List<RawColumn> Columns { get; } = [];

    private ArrayBufferWriter<byte> Data { get; } = new();

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Registers.TryGetValue(call.Function, out var register))
        {
            return;
        }

        var start = new RowBufferStart(call.Arguments[register]);

        if (start.Iterator >= SmallestAddress && TryReadLayout(replay, ref start))
        {
            Starts[call.Id] = start;
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Starts.Remove(call.Id, out var start))
        {
            return;
        }

        if (start.Layout is null && (!TryReadLayout(replay, ref start) || start.Layout is null))
        {
            return;
        }

        var isValueRow = start.Layout.Value.Format == RowBufferFormat.Values;

        if (start.Buffer == 0 || (isValueRow ? start.Count == 0 : start.Bindings == 0))
        {
            TryReadLayout(replay, ref start);
        }

        if (isValueRow)
        {
            ReadValueRow(call, replay, start);

            return;
        }

        if (start.Buffer < SmallestAddress || start.Bindings < SmallestAddress || ReadBindings(replay, start.Bindings) is not { } set)
        {
            return;
        }

        foreach (var binding in set.Columns)
        {
            Columns.Add(ReadColumn(replay, binding, start.Buffer));
        }

        AddRow(call, start, RowBufferFormat.Bindings);
    }

    public void OnAbandoned(in TraceHookCall call) => Starts.Remove(call.Id);

    private static bool IsInBuffer(ulong offset, ulong size) => offset < MaximumRowBufferBytes && size <= MaximumRowBufferBytes - offset;

    private static ColumnBinding ReadBinding(ITraceReplay replay, ulong address)
    {
        if (!replay.TryRead(address + BindingRecordOffset, out int recordOffset))
        {
            recordOffset = -1;
        }

        ushort fixedLength = 0;

        ulong valueOffset = 0;

        ulong statusOffset = 0;

        ulong lengthOffset = 0;

        byte infoFlags = 0;

        var isReadable = replay.TryRead(address + BindingFlagsOffset, out uint flags)
                         && replay.TryRead(address + BindingFixedLengthOffset, out fixedLength)
                         && replay.TryRead(address + BindingValueOffset, out valueOffset)
                         && replay.TryRead(address + BindingStatusOffset, out statusOffset)
                         && replay.TryRead(address + BindingInfoOffset, out ulong info)
                         && info >= SmallestAddress
                         && replay.TryRead(info + BindingInfoLengthOffset, out lengthOffset)
                         && replay.TryRead(info + BindingInfoFlagsOffset, out infoFlags);

        var unreadable = !isReadable ? RowBufferColumnFlags.BindingUnreadable
                         : (infoFlags & AlternateBufferBinding) != 0 ? RowBufferColumnFlags.AlternateBuffer
                         : RowBufferColumnFlags.None;

        return new ColumnBinding(recordOffset, flags, fixedLength, unreadable, valueOffset, statusOffset, lengthOffset);
    }

    private bool TryReadLayout(ITraceReplay replay, ref RowBufferStart start)
    {
        if (!replay.TryRead(start.Iterator, out ulong vtable))
        {
            return true;
        }

        if (!Layouts.TryGetValue(vtable, out var layout))
        {
            return false;
        }

        start.Layout = layout;

        if (replay.TryRead(start.Iterator + layout.BufferOffset, out ulong buffer))
        {
            start.Buffer = buffer;
        }

        if (layout.Format == RowBufferFormat.Values)
        {
            if (replay.TryRead(start.Iterator + layout.CountOffset, out uint count))
            {
                start.Count = count;
            }
        }
        else if (replay.TryRead(start.Iterator + layout.BindingsOffset, out ulong bindings))
        {
            start.Bindings = bindings;
        }

        return true;
    }

    private void ReadValueRow(in TraceHookCall call, ITraceReplay replay, RowBufferStart start)
    {
        if (start.Buffer < SmallestAddress || start.Count == 0 || start.Count > MaximumBindings)
        {
            return;
        }

        for (uint index = 0; index < start.Count; index++)
        {
            Columns.Add(ReadValueColumn(replay, start.Buffer + index * RowValueSize));
        }

        AddRow(call, start, RowBufferFormat.Values);
    }

    private RawColumn ReadValueColumn(ITraceReplay replay, ulong address)
    {
        var column = CreateColumn(-1, 0, 0);

        if (!replay.TryRead(address + RowValueStatusOffset, out byte status))
        {
            return column with { Flags = (byte)(RowBufferColumnFlags.Unreadable | RowBufferColumnFlags.SlotUnreadable) };
        }

        if (status != 0)
        {
            return column with { Status = status };
        }

        if (!replay.TryRead(address + RowValueDataOffset, out ulong value)
            || !replay.TryRead(address + RowValueLengthOffset, out ulong length))
        {
            return column with { Flags = (byte)(RowBufferColumnFlags.Unreadable | RowBufferColumnFlags.SlotUnreadable) };
        }

        if (length == 0)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(Data.GetSpan(sizeof(ulong)), value);

            Data.Advance(sizeof(ulong));

            return column with { Length = sizeof(ulong), Captured = sizeof(ulong) };
        }

        column = column with
        {
            Flags = (byte)(RowBufferColumnFlags.ByReference | RowBufferColumnFlags.HasLength),
            Length = (uint)Math.Min(length, uint.MaxValue)
        };

        return ReadValueBytes(replay, value, length, column);
    }

    private BindingSet? ReadBindings(ITraceReplay replay, ulong bindings)
    {
        if (!replay.TryRead(bindings + BindingCountOffset, out ushort count)
            || !replay.TryRead(bindings + BindingArrayOffset, out ulong array)
            || count == 0
            || count > MaximumBindings
            || array < SmallestAddress)
        {
            return null;
        }

        if (!replay.TryRead(array + BindingValueOffset, out ulong firstValueOffset)
            || !replay.TryRead(array + BindingFlagsOffset, out uint firstFlags))
        {
            return null;
        }

        if (BindingSets.TryGetValue(array, out var set)
            && set.Count == count
            && set.FirstValueOffset == firstValueOffset
            && set.FirstFlags == firstFlags)
        {
            return set;
        }

        set = new BindingSet(count,
                             firstValueOffset,
                             firstFlags,
                             [.. Enumerable.Range(0, count).Select(i => ReadBinding(replay, array + (ulong)i * BindingSize))]);

        BindingSets[array] = set;

        return set;
    }

    private RawColumn ReadColumn(ITraceReplay replay, ColumnBinding binding, ulong buffer)
    {
        var column = CreateColumn(binding.RecordOffset, binding.Flags, binding.FixedLength);

        if (binding.Unreadable != RowBufferColumnFlags.None)
        {
            return column with { Flags = (byte)(RowBufferColumnFlags.Unreadable | binding.Unreadable) };
        }

        if (!IsInBuffer(binding.StatusOffset, sizeof(byte))
            || !IsInBuffer(binding.ValueOffset, sizeof(ulong))
            || !replay.TryRead(buffer + binding.StatusOffset, out byte status))
        {
            return column with { Flags = (byte)(RowBufferColumnFlags.Unreadable | RowBufferColumnFlags.SlotUnreadable) };
        }

        if (status != 0)
        {
            return column with { Status = status };
        }

        ulong length = binding.FixedLength;

        var flags = RowBufferColumnFlags.None;

        if ((binding.Flags & LengthBinding) != 0)
        {
            if (!IsInBuffer(binding.LengthOffset, sizeof(ulong)) || !replay.TryRead(buffer + binding.LengthOffset, out length))
            {
                return column with { Flags = (byte)(RowBufferColumnFlags.Unreadable | RowBufferColumnFlags.SlotUnreadable) };
            }

            flags |= RowBufferColumnFlags.HasLength;
        }

        column = column with { Length = (uint)Math.Min(length, uint.MaxValue), Flags = (byte)flags };

        if ((binding.Flags & ByReferenceBinding) == 0)
        {
            var stored = binding.FixedLength > 0 ? Math.Min(length, binding.FixedLength) : length;

            return ReadValueBytes(replay, buffer + binding.ValueOffset, stored, column);
        }

        flags |= RowBufferColumnFlags.ByReference;

        column = column with { Flags = (byte)flags };

        if (!replay.TryRead(buffer + binding.ValueOffset, out ulong pointer))
        {
            return column with { Flags = (byte)(flags | RowBufferColumnFlags.Unreadable | RowBufferColumnFlags.SlotUnreadable) };
        }

        return TryReadPageReference(replay, pointer, length, ref column) ? column : ReadValueBytes(replay, pointer, length, column);
    }

    private RawColumn ReadValueBytes(ITraceReplay replay, ulong address, ulong length, RawColumn column)
    {
        var wanted = (int)Math.Min(length, MaximumValueBytes);

        var found = address < SmallestAddress || wanted == 0 ? 0 : replay.Read(address, Data.GetSpan(wanted)[..wanted]);

        Data.Advance(found);

        var flags = (RowBufferColumnFlags)column.Flags;

        if (found < wanted)
        {
            flags |= RowBufferColumnFlags.Unreadable;
        }
        else if ((uint)found < column.Length)
        {
            flags |= RowBufferColumnFlags.Truncated;
        }

        return column with { Captured = (uint)found, Flags = (byte)flags };
    }

    private bool TryReadPageReference(ITraceReplay replay, ulong pointer, ulong length, ref RawColumn column)
    {
        var offset = pointer & (DataPageSize - 1);

        if (pointer < SmallestAddress || offset < PageHeaderSize || length > DataPageSize - offset)
        {
            return false;
        }

        var page = pointer - offset;

        Span<byte> pageId = stackalloc byte[PageIdSize];

        Span<byte> lsn = stackalloc byte[LsnSize];

        if (replay.Read(page + PageIdOffset, pageId) != PageIdSize
            || replay.Read(page + LsnOffset, lsn) != LsnSize
            || BinaryPrimitives.ReadUInt16LittleEndian(pageId[PageIdFileOffset..]) == 0
            || (replay.TryRead(page, out byte version) && version != PageHeaderVersion))
        {
            return false;
        }

        var reference = Data.GetSpan(RowBufferPage.Size);

        BinaryPrimitives.WriteUInt16LittleEndian(reference, (ushort)offset);

        pageId.CopyTo(reference[sizeof(ushort)..]);

        lsn.CopyTo(reference[(sizeof(ushort) + PageIdSize)..]);

        Data.Advance(RowBufferPage.Size);

        column = column with
        {
            Captured = RowBufferPage.Size,
            Flags = (byte)((RowBufferColumnFlags)column.Flags | RowBufferColumnFlags.PageReference)
        };

        return true;
    }

    private RawColumn CreateColumn(int recordOffset, uint bindingFlags, ushort fixedLength)
        => new((uint)Data.WrittenCount, 0, 0, recordOffset, bindingFlags, fixedLength, 0, 0);

    private void AddRow(in TraceHookCall call, RowBufferStart start, RowBufferFormat format)
    {
        var row = new RawRow(call.Function,
                             call.Instance,
                             start.Iterator,
                             start.Buffer,
                             call.CallCount,
                             call.Call,
                             0,
                             (uint)Columns.Count,
                             format);

        log.Add([row], CollectionsMarshal.AsSpan(Columns), Data.WrittenSpan);

        Columns.Clear();

        Data.ResetWrittenCount();
    }

    private record struct RowBufferStart(ulong Iterator)
    {
        public RowBufferLayout? Layout { get; set; }

        public ulong Buffer { get; set; }

        public ulong Bindings { get; set; }

        public uint Count { get; set; }
    }

    private readonly record struct ColumnBinding(int RecordOffset,
                                                 uint Flags,
                                                 ushort FixedLength,
                                                 RowBufferColumnFlags Unreadable,
                                                 ulong ValueOffset,
                                                 ulong StatusOffset,
                                                 ulong LengthOffset);

    private sealed record BindingSet(ushort Count, ulong FirstValueOffset, uint FirstFlags, ColumnBinding[] Columns);
}
