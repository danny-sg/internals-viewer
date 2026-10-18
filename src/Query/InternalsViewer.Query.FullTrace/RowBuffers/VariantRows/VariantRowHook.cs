using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.Parsers;
using InternalsViewer.Query.FullTrace.RowBuffers.VariantRows.Enums;
using InternalsViewer.Query.FullTrace.RowSources;

namespace InternalsViewer.Query.FullTrace.RowBuffers.VariantRows;

internal sealed class VariantRowHook(VariantRowFunctions functions, RowSourceHook rowSources) : ITraceHook
{
    private const int ObjectArgument = 0;

    private const uint RowStatus = 0;

    private const ulong SlotOffset = 0x08;

    private const ulong WidthOffset = 0x0C;

    private const ulong HelperCountOffset = 0x50;

    private const ulong HelperRowOffset = 0x78;

    private const ulong HelperCursorOffset = 0x88;

    private const ulong HelperBatchOffset = 0x90;

    private const TraceHookEvents EntryAndReturn = TraceHookEvents.Entry | TraceHookEvents.Return;

    public IEnumerable<TraceHookFunction> Functions => [.. functions.Functions.Keys.Select(f => new TraceHookFunction(f, EntryAndReturn))];

    public IReadOnlyList<RowBuffer> Rows => Captured;

    private Dictionary<uint, Stack<OpenIterator>> OpenIterators { get; } = [];

    private Dictionary<ulong, ulong> Creating { get; } = [];

    private Dictionary<int, uint> Widths { get; } = [];

    private Dictionary<ulong, ulong> KnownRows { get; } = [];

    private Dictionary<ulong, uint> KnownCounts { get; } = [];

    private List<RowBuffer> Captured { get; } = [];

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!functions.Functions.TryGetValue(call.Function, out var type))
        {
            return;
        }

        switch (type)
        {
            case VariantRowFunctionType.Iterator or VariantRowFunctionType.BatchHelper:
                GetIterators(call.Thread).Push(new OpenIterator(call.Id, call.Arguments[ObjectArgument], type));
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

        if (Pop(call.Thread, call.Id) is not { } iterator || (uint)call.ReturnValue != RowStatus)
        {
            return;
        }

        if (iterator.Type == VariantRowFunctionType.BatchHelper)
        {
            CaptureHelperRow(call, iterator.Iterator, replay);
        }
        else
        {
            CaptureTableRows(call, iterator.Iterator, replay);
        }
    }

    public void OnAbandoned(in TraceHookCall call)
    {
        Creating.Remove(call.Id);

        Pop(call.Thread, call.Id);
    }

    private static (ulong Batch, int SelectionIndex) LocateBatchValues(ITraceReplay replay, ulong helper, RowBufferColumn[] columns)
    {
        if (!replay.TryRead(helper + HelperBatchOffset, out ulong batch) || batch < ColumnValueParser.SmallestAddress)
        {
            return (0, -1);
        }

        var selectionIndex = replay.TryRead(helper + HelperCursorOffset, out int cursor) && cursor > 0 ? cursor - 1 : -1;

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

    private void AddWidth(ITraceReplay replay, ulong table)
    {
        if (!replay.TryRead(table, out ulong vtable)
            || !functions.Classes.TryGetValue(vtable, out var tableClass)
            || !replay.TryRead(table + SlotOffset, out int slot)
            || !replay.TryRead(table + WidthOffset, out int value)
            || value < 0)
        {
            return;
        }

        var width = tableClass == VariantTableClass.Column ? (uint)value + 1 : (uint)value;

        Widths[slot] = Math.Max(Widths.GetValueOrDefault(slot), width);
    }

    private void CaptureTableRows(in TraceHookCall call, ulong iterator, ITraceReplay replay)
    {
        var captured = new HashSet<ulong>();

        foreach (var (context, slot) in rowSources.GetSlots(iterator))
        {
            if (!Widths.TryGetValue(slot, out var width)
                || GetRow(replay, context + (ulong)slot * sizeof(ulong)) is not { } row
                || !captured.Add(row)
                || VariantRowParser.Parse(replay, row, width) is not { } columns)
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

    private void CaptureHelperRow(in TraceHookCall call, ulong helper, ITraceReplay replay)
    {
        if (GetRow(replay, helper + HelperRowOffset) is not { } row
            || VariantRowParser.Parse(replay, row, GetCount(replay, helper)) is not { } columns)
        {
            return;
        }

        var (batch, selectionIndex) = LocateBatchValues(replay, helper, columns);

        Captured.Add(new RowBuffer(call.Function,
                                   call.Instance,
                                   (int)call.Call,
                                   call.Position,
                                   helper,
                                   row,
                                   columns,
                                   RowBufferFormat.Batch)
        {
            Batch = batch,
            BatchSelectionIndex = selectionIndex
        });
    }

    private ulong? GetRow(ITraceReplay replay, ulong location)
    {
        if (replay.TryRead(location, out ulong row) && row >= ColumnValueParser.SmallestAddress)
        {
            KnownRows[location] = row;

            return row;
        }

        return KnownRows.TryGetValue(location, out var known) ? known : null;
    }

    private uint GetCount(ITraceReplay replay, ulong helper)
    {
        if (replay.TryRead(helper + HelperCountOffset, out uint count) && count > 0)
        {
            KnownCounts[helper] = count;

            return count;
        }

        return KnownCounts.GetValueOrDefault(helper);
    }

    private OpenIterator? Pop(uint thread, ulong id)
    {
        if (!OpenIterators.TryGetValue(thread, out var iterators) || iterators.All(i => i.Id != id))
        {
            return null;
        }

        while (iterators.TryPop(out var open))
        {
            if (open.Id == id)
            {
                return open;
            }
        }

        return null;
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

    private readonly record struct OpenIterator(ulong Id, ulong Iterator, VariantRowFunctionType Type);
}
