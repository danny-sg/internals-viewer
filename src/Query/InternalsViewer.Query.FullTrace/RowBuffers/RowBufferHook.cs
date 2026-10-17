using InternalsViewer.Query.FullTrace.Batches;
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

    private const ulong LayoutBatchRowBufferOffset = 0x78;

    private const ulong LayoutBatchRowCountOffset = 0x50;

    private const ulong LayoutBatchRowCursorOffset = 0x88;

    private const ulong LayoutBatchRowBatchOffset = 0x90;

    public IEnumerable<TraceHookFunction> Functions
        => functions.Functions.Distinct().Select(f => new TraceHookFunction(f, TraceHookEvents.Entry | TraceHookEvents.Return));

    private IReadOnlyDictionary<ulong, RowBufferFormat> Formats => functions.Formats;

    private Dictionary<ulong, RowBufferStart> Starts { get; } = [];

    private Dictionary<(uint Thread, ulong Iterator), ulong> OpenCalls { get; } = [];

    private BindingParser Bindings { get; } = new();

    private Dictionary<ulong, RowBufferColumn[]> LastColumns { get; } = [];

    private Dictionary<ulong, RowBufferStart> KnownLayouts { get; } = [];

    private List<RowBuffer> Rows { get; } = [];

    public RowBufferStore Build(IEnumerable<RowBuffer>? others = null) => new([.. Rows, .. others ?? []]);

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        var start = new RowBufferStart(call.Arguments[IteratorArgument]);

        if (start.Iterator < ColumnValueParser.SmallestAddress || !TryReadIterator(replay, ref start))
        {
            return;
        }

        var key = (call.Thread, start.Iterator);

        if (OpenCalls.TryGetValue(key, out var outer))
        {
            Starts.Remove(outer);
        }

        OpenCalls[key] = call.Id;

        Starts[call.Id] = start;
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Remove(call.Thread, call.Id, out var start))
        {
            return;
        }

        if (start.Format is null && !TryReadIterator(replay, ref start))
        {
            return;
        }

        ApplyKnownLayout(ref start);

        if (start.Format is not { } format)
        {
            return;
        }

        var isBatchRow = format == RowBufferFormat.Batch;

        if (start.Buffer == 0 || (isBatchRow ? start.Count == 0 : start.Bindings == 0))
        {
            TryReadIterator(replay, ref start);

            ApplyKnownLayout(ref start);
        }

        var columns = isBatchRow ? BatchRowParser.Parse(replay, start.Buffer, start.Count) : ParseBoundRow(replay, start);

        if (columns is null)
        {
            return;
        }

        KnownLayouts[start.Iterator] = start;

        var (batch, selectionIndex) = isBatchRow ? LocateBatchValues(replay, start.Iterator, columns) : (0ul, -1);

        Rows.Add(new RowBuffer(call.Function,
                               call.Instance,
                               (int)call.Call,
                               call.Position,
                               start.Iterator,
                               start.Buffer,
                               GetShared(start.Iterator, columns),
                               format)
        {
            Batch = batch,
            BatchSelectionIndex = selectionIndex
        });
    }

    public void OnAbandoned(in TraceHookCall call) => Remove(call.Thread, call.Id, out _);

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
                || first.DeepDataVectorId != second.DeepDataVectorId
                || !first.Data.AsSpan().SequenceEqual(second.Data))
            {
                return false;
            }
        }

        return true;
    }

    private static (ulong Batch, int SelectionIndex) LocateBatchValues(ITraceReplay replay, ulong iterator, RowBufferColumn[] columns)
    {
        if (!replay.TryRead(iterator + LayoutBatchRowBatchOffset, out ulong batch) || batch < ColumnValueParser.SmallestAddress)
        {
            return (0, -1);
        }

        var selectionIndex = replay.TryRead(iterator + LayoutBatchRowCursorOffset, out int cursor) && cursor > 0 ? cursor - 1 : -1;

        List<(ulong Start, ulong End, int VectorId)>? pages = null;

        for (var index = 0; index < columns.Length; index++)
        {
            var pointer = columns[index].Pointer;

            if (pointer == 0)
            {
                continue;
            }

            pages ??= BatchDeepDataLocator.GetPages(replay, batch);

            foreach (var page in pages)
            {
                if (pointer >= page.Start && pointer < page.End)
                {
                    columns[index] = columns[index] with { DeepDataVectorId = page.VectorId };

                    break;
                }
            }
        }

        return (batch, selectionIndex);
    }

    private void ApplyKnownLayout(ref RowBufferStart start)
    {
        if (!KnownLayouts.TryGetValue(start.Iterator, out var known))
        {
            return;
        }

        start.Format ??= known.Format;

        if (start.Buffer == 0)
        {
            start.Buffer = known.Buffer;
        }

        if (start.Bindings == 0)
        {
            start.Bindings = known.Bindings;
        }

        if (start.Count == 0)
        {
            start.Count = known.Count;
        }
    }

    private bool Remove(uint thread, ulong id, out RowBufferStart start)
    {
        if (!Starts.Remove(id, out start))
        {
            return false;
        }

        var key = (thread, start.Iterator);

        if (OpenCalls.TryGetValue(key, out var open) && open == id)
        {
            OpenCalls.Remove(key);
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
            KnownLayouts.Remove(start.Iterator);

            return false;
        }

        start.Format = format;

        var isBatchRow = format == RowBufferFormat.Batch;

        if (replay.TryRead(start.Iterator + (isBatchRow ? LayoutBatchRowBufferOffset : LayoutRowsetBufferOffset), out ulong buffer))
        {
            start.Buffer = buffer;
        }

        if (isBatchRow)
        {
            if (replay.TryRead(start.Iterator + LayoutBatchRowCountOffset, out uint count))
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
