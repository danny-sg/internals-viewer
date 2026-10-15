using System.Buffers.Binary;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;

namespace InternalsViewer.Query.FullTrace.RowBuffers;

internal sealed class RowBufferHook(RowBufferFunctions functions) : ITraceHook
{
    private const ulong SmallestAddress = 0x10000;

    private const int IteratorArgument = 0;

    private const ulong RowsetBufferOffset = 0x80;

    private const ulong RowsetBindingsOffset = 0x88;

    private const ulong ValueRowBufferOffset = 0x78;

    private const ulong ValueRowCountOffset = 0x50;

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

    private const RowBufferColumnFlags SlotUnreadable = RowBufferColumnFlags.Unreadable | RowBufferColumnFlags.SlotUnreadable;

    public IEnumerable<TraceHookFunction> Functions
        => functions.Functions.Distinct().Select(f => new TraceHookFunction(f, TraceHookEvents.Entry | TraceHookEvents.Return));

    private Dictionary<ulong, RowBufferFormat> Formats { get; }
        = functions.Formats.GroupBy(f => f.VTable).ToDictionary(g => g.Key, g => g.Last().Format);

    private Dictionary<ulong, RowBufferStart> Starts { get; } = [];

    private Dictionary<ulong, BindingSet> BindingSets { get; } = [];

    private Dictionary<ulong, RowBufferColumn[]> LastColumns { get; } = [];

    private List<RowBuffer> Rows { get; } = [];

    public RowBufferIndex Build() => new(Rows);

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        var start = new RowBufferStart(call.Arguments[IteratorArgument]);

        if (start.Iterator >= SmallestAddress && TryReadIterator(replay, ref start))
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

        if ((start.Format is null && !TryReadIterator(replay, ref start)) || start.Format is not { } format)
        {
            return;
        }

        var isValueRow = format == RowBufferFormat.Values;

        if (start.Buffer == 0 || (isValueRow ? start.Count == 0 : start.Bindings == 0))
        {
            TryReadIterator(replay, ref start);
        }

        if ((isValueRow ? ReadValueRow(replay, start) : ReadBindingRow(replay, start)) is not { } columns)
        {
            return;
        }

        Rows.Add(new RowBuffer(call.Function,
                               call.Instance,
                               (int)call.Call,
                               call.CallCount,
                               start.Iterator,
                               start.Buffer,
                               GetShared(start.Iterator, columns),
                               format));
    }

    public void OnAbandoned(in TraceHookCall call) => Starts.Remove(call.Id);

    private static bool IsInBuffer(ulong offset, ulong size) => offset < MaximumRowBufferBytes && size <= MaximumRowBufferBytes - offset;

    private static int GetLength(ulong length) => (int)(uint)Math.Min(length, uint.MaxValue);

    private static RowBufferColumn CreateColumn(int ordinal, RowBufferBinding binding)
        => new(ordinal, binding, 0, RowBufferColumnFlags.None, 0, []);

    private static bool IsSame(RowBufferColumn[] left, RowBufferColumn[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var ordinal = 0; ordinal < left.Length; ordinal++)
        {
            var (first, second) = (left[ordinal], right[ordinal]);

            if (first.Binding != second.Binding
                || first.Status != second.Status
                || first.Flags != second.Flags
                || first.Length != second.Length
                || first.Page != second.Page
                || !first.Data.AsSpan().SequenceEqual(second.Data))
            {
                return false;
            }
        }

        return true;
    }

    private static RowBufferColumn[]? ReadValueRow(ITraceReplay replay, RowBufferStart start)
    {
        if (start.Buffer < SmallestAddress || start.Count == 0 || start.Count > MaximumBindings)
        {
            return null;
        }

        var columns = new RowBufferColumn[start.Count];

        for (var index = 0; index < columns.Length; index++)
        {
            columns[index] = ReadValueColumn(replay, index, start.Buffer + (ulong)index * RowValueSize);
        }

        return columns;
    }

    private static RowBufferColumn ReadValueColumn(ITraceReplay replay, int ordinal, ulong address)
    {
        var column = CreateColumn(ordinal, RowBufferBinding.None);

        if (!replay.TryRead(address + RowValueStatusOffset, out byte status))
        {
            return column with { Flags = SlotUnreadable };
        }

        if (status != 0)
        {
            return column with { Status = status };
        }

        if (!replay.TryRead(address + RowValueDataOffset, out ulong value)
            || !replay.TryRead(address + RowValueLengthOffset, out ulong length))
        {
            return column with { Flags = SlotUnreadable };
        }

        if (length == 0)
        {
            return column with { Length = sizeof(ulong), Data = BitConverter.GetBytes(value) };
        }

        column = column with
        {
            Flags = RowBufferColumnFlags.ByReference | RowBufferColumnFlags.HasLength,
            Length = GetLength(length)
        };

        return ReadValueBytes(replay, value, length, column);
    }

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

        return new ColumnBinding(new RowBufferBinding(recordOffset, flags, fixedLength),
                                 unreadable,
                                 valueOffset,
                                 statusOffset,
                                 lengthOffset);
    }

    private static RowBufferColumn ReadColumn(ITraceReplay replay, int ordinal, ColumnBinding binding, ulong buffer)
    {
        var column = CreateColumn(ordinal, binding.Binding);

        if (binding.Unreadable != RowBufferColumnFlags.None)
        {
            return column with { Flags = RowBufferColumnFlags.Unreadable | binding.Unreadable };
        }

        if (!IsInBuffer(binding.StatusOffset, sizeof(byte))
            || !IsInBuffer(binding.ValueOffset, sizeof(ulong))
            || !replay.TryRead(buffer + binding.StatusOffset, out byte status))
        {
            return column with { Flags = SlotUnreadable };
        }

        if (status != 0)
        {
            return column with { Status = status };
        }

        var length = (ulong)binding.Binding.FixedLength;

        var flags = RowBufferColumnFlags.None;

        if ((binding.Binding.Flags & LengthBinding) != 0)
        {
            if (!IsInBuffer(binding.LengthOffset, sizeof(ulong)) || !replay.TryRead(buffer + binding.LengthOffset, out length))
            {
                return column with { Flags = SlotUnreadable };
            }

            flags |= RowBufferColumnFlags.HasLength;
        }

        column = column with { Length = GetLength(length), Flags = flags };

        if ((binding.Binding.Flags & ByReferenceBinding) == 0)
        {
            var stored = binding.Binding.FixedLength > 0 ? Math.Min(length, (ulong)binding.Binding.FixedLength) : length;

            return ReadValueBytes(replay, buffer + binding.ValueOffset, stored, column);
        }

        column = column with { Flags = flags | RowBufferColumnFlags.ByReference };

        if (!replay.TryRead(buffer + binding.ValueOffset, out ulong pointer))
        {
            return column with { Flags = column.Flags | SlotUnreadable };
        }

        return ReadPage(replay, pointer, length) is { } page
            ? column with { Flags = column.Flags | RowBufferColumnFlags.PageReference, Page = page }
            : ReadValueBytes(replay, pointer, length, column);
    }

    private static RowBufferColumn ReadValueBytes(ITraceReplay replay, ulong address, ulong length, RowBufferColumn column)
    {
        var data = new byte[(int)Math.Min(length, MaximumValueBytes)];

        var found = address < SmallestAddress || data.Length == 0 ? 0 : replay.Read(address, data);

        var flags = column.Flags;

        if (found < data.Length)
        {
            flags |= RowBufferColumnFlags.Unreadable;
        }
        else if ((uint)found < (uint)column.Length)
        {
            flags |= RowBufferColumnFlags.Truncated;
        }

        return column with { Data = found == data.Length ? data : data[..found], Flags = flags };
    }

    private static RowBufferPage? ReadPage(ITraceReplay replay, ulong pointer, ulong length)
    {
        var offset = pointer & (DataPageSize - 1);

        if (pointer < SmallestAddress || offset < PageHeaderSize || length > DataPageSize - offset)
        {
            return null;
        }

        var page = pointer - offset;

        Span<byte> pageId = stackalloc byte[PageIdSize];

        Span<byte> lsn = stackalloc byte[LsnSize];

        if (replay.Read(page + PageIdOffset, pageId) != PageIdSize
            || replay.Read(page + LsnOffset, lsn) != LsnSize
            || BinaryPrimitives.ReadUInt16LittleEndian(pageId[PageIdFileOffset..]) == 0
            || (replay.TryRead(page, out byte version) && version != PageHeaderVersion))
        {
            return null;
        }

        return new RowBufferPage(PageAddressParser.Parse(pageId), (int)offset, LogSequenceNumberParser.Parse(lsn));
    }

    private RowBufferColumn[] GetShared(ulong iterator, RowBufferColumn[] columns)
    {
        if (LastColumns.TryGetValue(iterator, out var last) && IsSame(last, columns))
        {
            return last;
        }

        LastColumns[iterator] = columns;

        return columns;
    }

    private bool TryReadIterator(ITraceReplay replay, ref RowBufferStart start)
    {
        if (!replay.TryRead(start.Iterator, out ulong vtable))
        {
            return true;
        }

        if (!Formats.TryGetValue(vtable, out var format))
        {
            return false;
        }

        start.Format = format;

        var isValueRow = format == RowBufferFormat.Values;

        if (replay.TryRead(start.Iterator + (isValueRow ? ValueRowBufferOffset : RowsetBufferOffset), out ulong buffer))
        {
            start.Buffer = buffer;
        }

        if (isValueRow)
        {
            if (replay.TryRead(start.Iterator + ValueRowCountOffset, out uint count))
            {
                start.Count = count;
            }
        }
        else if (replay.TryRead(start.Iterator + RowsetBindingsOffset, out ulong bindings))
        {
            start.Bindings = bindings;
        }

        return true;
    }

    private RowBufferColumn[]? ReadBindingRow(ITraceReplay replay, RowBufferStart start)
    {
        if (start.Buffer < SmallestAddress || start.Bindings < SmallestAddress || ReadBindings(replay, start.Bindings) is not { } set)
        {
            return null;
        }

        var columns = new RowBufferColumn[set.Columns.Length];

        for (var ordinal = 0; ordinal < columns.Length; ordinal++)
        {
            columns[ordinal] = ReadColumn(replay, ordinal, set.Columns[ordinal], start.Buffer);
        }

        return columns;
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

    private record struct RowBufferStart(ulong Iterator)
    {
        public RowBufferFormat? Format { get; set; }

        public ulong Buffer { get; set; }

        public ulong Bindings { get; set; }

        public uint Count { get; set; }
    }

    private readonly record struct ColumnBinding(RowBufferBinding Binding,
                                                 RowBufferColumnFlags Unreadable,
                                                 ulong ValueOffset,
                                                 ulong StatusOffset,
                                                 ulong LengthOffset);

    private sealed record BindingSet(ushort Count, ulong FirstValueOffset, uint FirstFlags, ColumnBinding[] Columns);
}
