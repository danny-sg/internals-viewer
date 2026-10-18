using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.Parsers;
using InternalsViewer.Query.FullTrace.RowBuffers.VariantTables.Enums;

namespace InternalsViewer.Query.FullTrace.RowBuffers.VariantTables;

internal sealed class VariantTableHook(VariantTableFunctions functions) : ITraceHook
{
    private const int ObjectArgument = 0;

    private const int ContextArgument = 1;

    private const uint RowStatus = 0;

    private const ulong LayoutSlotOffset = 0x08;

    private const ulong LayoutWidthOffset = 0x0C;

    private const TraceHookEvents EntryAndReturn = TraceHookEvents.Entry | TraceHookEvents.Return;

    public IEnumerable<TraceHookFunction> Functions => [.. functions.Functions.Keys.Select(f => new TraceHookFunction(f, EntryAndReturn))];

    public IReadOnlyList<RowBuffer> Rows => Captured;

    private Dictionary<uint, Stack<OpenIterator>> OpenIterators { get; } = [];

    private Dictionary<ulong, ulong> Creating { get; } = [];

    private Dictionary<int, uint> Widths { get; } = [];

    private Dictionary<ulong, HashSet<(ulong Context, int Slot)>> IteratorSlots { get; } = [];

    private Dictionary<(ulong Context, int Slot), ulong> KnownRows { get; } = [];

    private List<RowBuffer> Captured { get; } = [];

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!functions.Functions.TryGetValue(call.Function, out var type))
        {
            return;
        }

        switch (type)
        {
            case VariantTableFunctionType.Iterator:
                GetIterators(call.Thread).Push(new OpenIterator(call.Id, call.Arguments[ObjectArgument]));
                break;

            case VariantTableFunctionType.Writer:
                AddWriter(call, replay);
                break;

            default:
                Creating[call.Id] = call.Arguments[ObjectArgument];
                break;
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (Creating.Remove(call.Id, out var created))
        {
            AddWidth(replay, created);

            return;
        }

        if (Pop(call.Thread, call.Id) is { } iterator && (uint)call.ReturnValue == RowStatus)
        {
            Capture(call, iterator, replay);
        }
    }

    public void OnAbandoned(in TraceHookCall call)
    {
        Creating.Remove(call.Id);

        Pop(call.Thread, call.Id);
    }

    private void AddWriter(in TraceHookCall call, ITraceReplay replay)
    {
        var writer = call.Arguments[ObjectArgument];

        if (!OpenIterators.TryGetValue(call.Thread, out var iterators)
            || !iterators.TryPeek(out var open)
            || !AddWidth(replay, writer)
            || !replay.TryRead(writer + LayoutSlotOffset, out int slot))
        {
            return;
        }

        if (!IteratorSlots.TryGetValue(open.Iterator, out var slots))
        {
            slots = [];

            IteratorSlots[open.Iterator] = slots;
        }

        slots.Add((call.Arguments[ContextArgument], slot));
    }

    private bool AddWidth(ITraceReplay replay, ulong table)
    {
        if (!replay.TryRead(table, out ulong vtable)
            || !functions.Classes.TryGetValue(vtable, out var tableClass)
            || !replay.TryRead(table + LayoutSlotOffset, out int slot)
            || !replay.TryRead(table + LayoutWidthOffset, out int value)
            || value < 0)
        {
            return false;
        }

        var width = tableClass == VariantTableClass.Column ? (uint)value + 1 : (uint)value;

        Widths[slot] = Math.Max(Widths.GetValueOrDefault(slot), width);

        return true;
    }

    private void Capture(in TraceHookCall call, ulong iterator, ITraceReplay replay)
    {
        if (!IteratorSlots.TryGetValue(iterator, out var slots))
        {
            return;
        }

        var captured = new HashSet<ulong>();

        foreach (var key in slots)
        {
            if (!Widths.TryGetValue(key.Slot, out var width) || GetRow(replay, key) is not { } row || !captured.Add(row))
            {
                continue;
            }

            if (BatchRowParser.Parse(replay, row, width) is not { } columns)
            {
                continue;
            }

            Captured.Add(new RowBuffer(call.Function,
                                       call.Instance,
                                       (int)call.Call,
                                       call.Position,
                                       iterator,
                                       row,
                                       columns,
                                       RowBufferFormat.VariantTable));
        }
    }

    private ulong? GetRow(ITraceReplay replay, (ulong Context, int Slot) key)
    {
        if (replay.TryRead(key.Context + (ulong)key.Slot * sizeof(ulong), out ulong row) && row >= ColumnValueParser.SmallestAddress)
        {
            KnownRows[key] = row;

            return row;
        }

        return KnownRows.TryGetValue(key, out var known) ? known : null;
    }

    private ulong? Pop(uint thread, ulong id)
    {
        if (!OpenIterators.TryGetValue(thread, out var iterators) || !iterators.TryPeek(out var open) || open.Id != id)
        {
            return null;
        }

        iterators.Pop();

        return open.Iterator;
    }

    private Stack<OpenIterator> GetIterators(uint thread)
    {
        if (!OpenIterators.TryGetValue(thread, out var iterators))
        {
            iterators = [];

            OpenIterators[thread] = iterators;
        }

        return iterators;
    }

    private readonly record struct OpenIterator(ulong Id, ulong Iterator);
}
