using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

namespace InternalsViewer.Query.FullTrace.RowBuffers;

internal sealed class RowBufferHook(RowBufferFunctions functions) : ITraceHook
{
    private const int IteratorArgument = 0;

    private const ulong LayoutRowsetBufferOffset = 0x80;

    private const ulong LayoutRowsetBindingsOffset = 0x88;

    private const ulong LayoutValueRowBufferOffset = 0x78;

    private const ulong LayoutValueRowCountOffset = 0x50;

    public IEnumerable<TraceHookFunction> Functions
        => functions.Functions.Distinct().Select(f => new TraceHookFunction(f, TraceHookEvents.Entry | TraceHookEvents.Return));

    private Dictionary<ulong, RowBufferFormat> Formats { get; }
        = functions.Formats.GroupBy(f => f.VTable).ToDictionary(g => g.Key, g => g.Last().Format);

    private Dictionary<ulong, RowBufferStart> Starts { get; } = [];

    private BindingParser Bindings { get; } = new();

    private Dictionary<ulong, RowBufferColumn[]> LastColumns { get; } = [];

    private List<RowBuffer> Rows { get; } = [];

    public RowBufferStore Build() => new(Rows);

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        var start = new RowBufferStart(call.Arguments[IteratorArgument]);

        if (start.Iterator >= ColumnValueParser.SmallestAddress && TryReadIterator(replay, ref start))
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

        var columns = isValueRow ? ValueRowParser.Parse(replay, start.Buffer, start.Count) : ParseBoundRow(replay, start);

        if (columns is null)
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
                || first.Deferred != second.Deferred
                || first.Pointer != second.Pointer
                || !first.Data.AsSpan().SequenceEqual(second.Data))
            {
                return false;
            }
        }

        return true;
    }

    private RowBufferColumn[]? ParseBoundRow(ITraceReplay replay, RowBufferStart start)
    {
        if (start.Buffer < ColumnValueParser.SmallestAddress
            || start.Bindings < ColumnValueParser.SmallestAddress
            || Bindings.Parse(replay, start.Bindings) is not { } set)
        {
            return null;
        }

        return BoundRowParser.Parse(replay, set, start.Buffer);
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

        if (replay.TryRead(start.Iterator + (isValueRow ? LayoutValueRowBufferOffset : LayoutRowsetBufferOffset), out ulong buffer))
        {
            start.Buffer = buffer;
        }

        if (isValueRow)
        {
            if (replay.TryRead(start.Iterator + LayoutValueRowCountOffset, out uint count))
            {
                start.Count = count;
            }
        }
        else if (replay.TryRead(start.Iterator + LayoutRowsetBindingsOffset, out ulong bindings))
        {
            start.Bindings = bindings;
        }

        return true;
    }

    private record struct RowBufferStart(ulong Iterator)
    {
        public RowBufferFormat? Format { get; set; }

        public ulong Buffer { get; set; }

        public ulong Bindings { get; set; }

        public uint Count { get; set; }
    }
}
