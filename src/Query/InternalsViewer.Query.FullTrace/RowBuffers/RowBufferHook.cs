using InternalsViewer.Query.FullTrace.RowBuffers.DeferredValues;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.RowBuffers.Accessors;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

namespace InternalsViewer.Query.FullTrace.RowBuffers;

internal sealed class RowBufferHook(RowBufferFunctions functions, AccessorHook accessors) : ITraceHook
{
    private const int IteratorArgument = 0;

    private const ulong RowsetBufferOffset = 0x80;

    private const ulong RowsetBindingsOffset = 0x88;

    public IEnumerable<TraceHookFunction> Functions
        => functions.Functions.Distinct().Select(f => new TraceHookFunction(f, TraceHookEvents.Entry | TraceHookEvents.Return));

    private HashSet<ulong> Rowsets { get; } = [.. functions.Rowsets];

    private Dictionary<ulong, RowBufferStart> Starts { get; } = [];

    private Dictionary<(uint Thread, ulong Iterator), ulong> OpenCalls { get; } = [];

    private BindingParser Bindings { get; } = new();

    private Dictionary<ulong, RowBufferColumn[]> LastColumns { get; } = [];

    private Dictionary<ulong, RowBufferStart> KnownLayouts { get; } = [];

    private Dictionary<(ulong Iterator, RowBufferBinding Binding), RowBufferBinding> TypedBindings { get; } = [];

    private List<RowBuffer> Rows { get; } = [];

    private Dictionary<int, RowLocationParser.RowPosition> Positions { get; } = [];

    private Dictionary<ulong, PageAddress> KnownPages { get; } = [];

    public RowBufferStore Build(IEnumerable<RowBuffer>? others = null) => new([.. GetLocatedRows(), .. others ?? []]);

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

        if (!start.IsRowset && !TryReadIterator(replay, ref start))
        {
            return;
        }

        ApplyKnownLayout(ref start);

        if (!start.IsRowset)
        {
            return;
        }

        if (start.Buffer == 0 || start.Bindings == 0)
        {
            TryReadIterator(replay, ref start);

            ApplyKnownLayout(ref start);
        }

        if (ParseBoundRow(replay, start) is not { } columns)
        {
            return;
        }

        KnownLayouts[start.Iterator] = start;

        if (RowLocationParser.Parse(replay, start.Iterator, functions.RowObjects) is { } position)
        {
            if (position.Page is { } page)
            {
                KnownPages[position.Frame] = page;
            }

            Positions[Rows.Count] = position;
        }

        Rows.Add(new RowBuffer(call.Function,
                               call.Instance,
                               (int)call.Call,
                               call.Position,
                               start.Iterator,
                               start.Buffer,
                               GetShared(start.Iterator, columns)));
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

    private void ApplyKnownLayout(ref RowBufferStart start)
    {
        if (!KnownLayouts.TryGetValue(start.Iterator, out var known))
        {
            return;
        }

        start.IsRowset = true;

        if (start.Buffer == 0)
        {
            start.Buffer = known.Buffer;
        }

        if (start.Bindings == 0)
        {
            start.Bindings = known.Bindings;
        }
    }

    private IEnumerable<RowBuffer> GetLocatedRows()
        => Rows.Select((r, i) => Positions.TryGetValue(i, out var position) && GetPage(position) is { } page
                                     ? r with { Location = new DeferredRecordValue(page, position.Slot) }
                                     : r);

    private PageAddress? GetPage(RowLocationParser.RowPosition position)
        => position.Page ?? (KnownPages.TryGetValue(position.Frame, out var page) ? page : null);

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

        var columns = BoundRowParser.Parse(replay, set, start.Buffer);

        for (var index = 0; index < columns.Length; index++)
        {
            columns[index] = columns[index] with { Binding = GetTypedBinding(start.Iterator, columns[index].Binding) };
        }

        return columns;
    }

    private RowBufferBinding GetTypedBinding(ulong iterator, RowBufferBinding binding)
    {
        if (binding.Type is not null)
        {
            return binding;
        }

        if (!TypedBindings.TryGetValue((iterator, binding), out var typed))
        {
            typed = accessors.GetColumnType(iterator, binding.ValueOffset) is { } type ? binding with { Type = type } : binding;

            TypedBindings[(iterator, binding)] = typed;
        }

        return typed;
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

        if (!Rowsets.Contains(vtable))
        {
            KnownLayouts.Remove(start.Iterator);

            return false;
        }

        start.IsRowset = true;

        if (replay.TryRead(start.Iterator + RowsetBufferOffset, out ulong buffer))
        {
            start.Buffer = buffer;
        }

        if (replay.TryRead(start.Iterator + RowsetBindingsOffset, out ulong bindings))
        {
            start.Bindings = bindings;
        }

        return true;
    }

    private record struct RowBufferStart(ulong Iterator)
    {
        public bool IsRowset { get; set; }

        public ulong Buffer { get; set; }

        public ulong Bindings { get; set; }
    }
}
