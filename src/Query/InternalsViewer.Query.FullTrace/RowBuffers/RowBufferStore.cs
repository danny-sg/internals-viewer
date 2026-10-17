using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.RowBuffers.DeferredValues;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.Parsers;
using InternalsViewer.Query.FullTrace.RowBuffers.Records;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.RowBuffers;

public sealed partial class RowBufferStore
{
    private const ulong SmallestSlot = sizeof(ulong);

    private const ulong DeepValueTag = 1;

    internal RowBufferStore(IEnumerable<RowBuffer> rows)
    {
        Rows = rows.GroupBy(r => (r.Function, r.Instance)).ToDictionary(g => g.Key, g => g.OrderBy(r => r.Call).ToArray());

        Count = Rows.Values.Sum(r => r.Length);
    }

    public static RowBufferStore Empty { get; } = new([]);

    internal IEnumerable<RowBuffer> All => Rows.Values.SelectMany(r => r);

    public int Count { get; }

    private Dictionary<(ulong Function, ulong Instance), RowBuffer[]> Rows { get; }

    private RowBufferRange[] Ranges
        => field ??= [.. Rows.Values
                             .SelectMany(r => r)
                             .GroupBy(r => r.Iterator)
                             .Select(g => g.First())
                             .Select(r => new RowBufferRange(r.Address, r.Address + GetExtent(r), r))
                             .OrderBy(r => r.Start)];

    public bool HasRows(ulong address, ulong instance) => Rows.ContainsKey((address, instance));

    public bool HasRow(ulong address, ulong instance, int call) => GetRow(address, instance, call) is not null;

    public IReadOnlyList<RowBuffer> GetRows(ulong address, ulong instance) => Rows.GetValueOrDefault((address, instance)) ?? [];

    public RowBuffer? GetRow(ulong address, ulong instance, int call)
    {
        var rows = GetRows(address, instance);

        var index = SortedSearch.FirstAtOrAfter(rows, call, r => r.Call);

        return index < rows.Count && rows[index].Call == call ? rows[index] : null;
    }

    public void ApplyRecordLayouts(Func<ulong, int, ColumnStructure?> getColumn, Func<ulong, int>? getKeyCount = null)
    {
        var layouts = new Dictionary<(ulong Iterator, int Count), RowRecordLayout?>();

        foreach (var rows in Rows.Values)
        {
            for (var index = 0; index < rows.Length; index++)
            {
                if (rows[index] is not { Format: RowBufferFormat.HashTable, Record: { } record } row)
                {
                    continue;
                }

                var key = (row.Iterator, RecordRowParser.GetColumnCount(record));

                if (!layouts.TryGetValue(key, out var layout))
                {
                    layout = RowRecordLayout.Create(key.Item2, getKeyCount?.Invoke(row.Iterator) ?? 0, o => getColumn(row.Iterator, o));

                    layouts[key] = layout;
                }

                if (layout is not null && RecordRowParser.Fits(record, layout))
                {
                    rows[index] = row with { Columns = RecordRowParser.Split(record, layout), RecordLayout = layout };
                }
            }
        }
    }

    public int GetColumnCount(ulong address, ulong instance)
        => GetRows(address, instance).Select(r => r.Columns.Count).DefaultIfEmpty().Max();

    public RowBufferSource GetSource(RowBuffer row, RowBufferColumn column, BatchStore? batches = null) => column.Deferred switch
    {
        _ when row.Format == RowBufferFormat.HashTable
            => new RowBufferSource(RowBufferSourceType.HashTable, row.Address, row.Iterator, column.Binding.ValueOffset, column.Ordinal),
        DeferredPageValue 
            => new RowBufferSource(RowBufferSourceType.BufferPool, column.Pointer),
        DeferredLobValue 
            => new RowBufferSource(RowBufferSourceType.Lob),
        _ when column.Pointer == 0 => new RowBufferSource(RowBufferSourceType.Inline),
        _ when column.DeepDataVectorId > 0 => GetVectorSource(row, column, batches),
        _ => GetRowBufferSource(column.Pointer)
             ?? GetBatchSource(column.Pointer, batches)
             ?? new RowBufferSource(RowBufferSourceType.Memory, column.Pointer)
    };

    internal void AnnotateCallStackNodes(TraceCallStore store)
    {
        foreach (var ((address, instance), rows) in Rows)
        {
            if (store.GetCallList(address, instance) is not { } calls)
            {
                continue;
            }

            foreach (var row in rows)
            {
                if (row.Call < calls.Count && store.GetNode(calls.GetNodeIndex(row.Call)) is { } node)
                {
                    node.HasRowBuffers = true;
                }
            }
        }
    }

    private static ulong GetExtent(RowBuffer row) => row.Format switch
    {
        RowBufferFormat.Batch
            => (ulong)row.Columns.Count * BatchRowParser.LayoutValueSize,
        RowBufferFormat.HashTable
            => (ulong)(row.Record?.Length ?? 0),
        _ => row.Columns.Select(c => c.Binding.ValueOffset + Math.Max((ulong)c.Binding.FixedLength, SmallestSlot)).DefaultIfEmpty().Max()
    };

    internal static int GetColumn(RowBuffer row, ulong offset)
    {
        if (row.Format == RowBufferFormat.Batch)
        {
            return (int)(offset / BatchRowParser.LayoutValueSize);
        }

        foreach (var column in row.Columns)
        {
            if (offset >= column.Binding.ValueOffset
                && offset < column.Binding.ValueOffset + Math.Max((ulong)column.Binding.FixedLength, SmallestSlot))
            {
                return column.Ordinal;
            }
        }

        return -1;
    }

    private static RowBufferSource? GetBatchSource(ulong pointer, BatchStore? batches)
        => batches?.GetDeepValue(pointer) is { } deepValue
            ? new RowBufferSource(RowBufferSourceType.Batch, pointer, deepValue.Capture.Iterator, 0, deepValue.VectorId, deepValue.Row)
            : null;

    private static RowBufferSource GetVectorSource(RowBuffer row, RowBufferColumn column, BatchStore? batches)
    {
        var handoff = batches?.GetHandoff(row.Batch, row.Iterator, row.Position);

        var index = row.BatchSelectionIndex;

        var batchRow = handoff is not null && index >= 0 && index < handoff.Selection.Length ? handoff.Selection[index] : -1;

        var slot = handoff is null || batchRow < 0 ? null : batches!.GetVectorSlot(handoff, column.DeepDataVectorId, batchRow);

        return new RowBufferSource(RowBufferSourceType.Batch,
                                   column.Pointer,
                                   handoff?.Iterator ?? 0,
                                   0,
                                   column.DeepDataVectorId,
                                   batchRow)
        {
            IsFromDictionary = slot is { } value && (value & DeepValueTag) == 0
        };
    }

    private RowBufferSource? GetRowBufferSource(ulong pointer)
    {
        var index = SortedSearch.FirstAtOrAfter(Ranges, pointer + 1, r => r.Start) - 1;

        if (index < 0 || pointer >= Ranges[index].End)
        {
            return null;
        }

        var range = Ranges[index];

        var offset = pointer - range.Start;

        return new RowBufferSource(RowBufferSourceType.RowBuffer, pointer, range.Row.Iterator, offset, GetColumn(range.Row, offset));
    }

    private readonly record struct RowBufferRange(ulong Start, ulong End, RowBuffer Row);
}
