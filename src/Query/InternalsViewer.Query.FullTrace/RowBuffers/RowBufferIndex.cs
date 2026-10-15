using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.RowBuffers;

public sealed partial class RowBufferIndex
{
    private RowBufferIndex(Dictionary<(ulong Address, ulong Instance), RowBufferTable> tables)
    {
        Tables = tables;

        Count = tables.Values.Sum(t => t.Count);
    }

    public static RowBufferIndex Empty { get; } = new([]);

    public int Count { get; }

    private Dictionary<(ulong Address, ulong Instance), RowBufferTable> Tables { get; }

    private Dictionary<ulong, IteratorTimeline> Timelines => field ??= BuildTimelines();

    public bool HasRows(ulong address, ulong instance) => Tables.ContainsKey((address, instance));

    public bool HasRow(ulong address, ulong instance, int call) => TryGetIterator(address, instance, call, out _);

    public RowBuffer? GetRow(ulong address, ulong instance, int call)
        => Tables.TryGetValue((address, instance), out var table) ? table.GetRow(call) : null;

    public bool TryGetIterator(ulong address, ulong instance, int call, out ulong iterator)
    {
        iterator = 0;

        return Tables.TryGetValue((address, instance), out var table) && table.TryGetIterator(call, out iterator);
    }

    public IReadOnlyList<int> GetCalls(ulong address, ulong instance)
        => Tables.TryGetValue((address, instance), out var table) ? table.GetCalls() : [];

    public int GetColumnCount(ulong address, ulong instance)
        => Tables.TryGetValue((address, instance), out var table) ? table.ColumnCount : 0;

    public IReadOnlyList<RowBuffer> GetRows(ulong address, ulong instance)
        => Tables.TryGetValue((address, instance), out var table) ? [.. table.GetRows()] : [];

    public RowBuffer? GetRowAt(ulong iterator, ulong callId)
    {
        if (!Timelines.TryGetValue(iterator, out var timeline))
        {
            return null;
        }

        var index = SortedSearch.FirstAtOrAfter(timeline.Ids, callId) - 1;

        return index < 0 ? null : timeline.Rows[index].Table.CreateRow(timeline.Rows[index].Index);
    }

    internal void AnnotateNodes(TraceCallStore store)
    {
        foreach (var table in Tables.Values)
        {
            if (store.GetCallList(table.Function, table.Instance) is not { } calls)
            {
                continue;
            }

            foreach (var call in table.GetCalls())
            {
                if (call < calls.Count && store.GetNode(calls.GetNodeIndex(call)) is { } node)
                {
                    node.HasRowBuffers = true;
                }
            }
        }
    }

    private Dictionary<ulong, IteratorTimeline> BuildTimelines()
    {
        var captures = new Dictionary<ulong, List<(ulong Id, RowBufferTable Table, int Index)>>();

        foreach (var table in Tables.Values)
        {
            for (var index = 0; index < table.Count; index++)
            {
                var (iterator, id) = table.GetCapture(index);

                ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(captures, iterator, out _);

                (list ??= []).Add((id, table, index));
            }
        }

        return captures.ToDictionary(c => c.Key, c => IteratorTimeline.Build(c.Value));
    }

    internal sealed class Log
    {
        private Dictionary<(ulong Address, ulong Instance), RowBufferTable.Builder> Builders { get; } = [];

        public unsafe void Add(IntPtr rows, int rowCount, IntPtr columns, int columnCount, IntPtr data, int dataLength)
            => Add(new ReadOnlySpan<BufferRow>((void*)rows, rowCount),
                   new ReadOnlySpan<BufferColumn>((void*)columns, columnCount),
                   new ReadOnlySpan<byte>((void*)data, dataLength));

        public void Add(ReadOnlySpan<BufferRow> rows, ReadOnlySpan<BufferColumn> columns, ReadOnlySpan<byte> data)
        {
            foreach (var row in rows)
            {
                if (!IsValid(row, columns, data.Length))
                {
                    continue;
                }

                ref var builder = ref CollectionsMarshal.GetValueRefOrAddDefault(Builders, (row.Function, row.Instance), out _);

                builder ??= new RowBufferTable.Builder(row.Function, row.Instance);

                builder.Add(row, columns.Slice((int)row.FirstColumn, (int)row.ColumnCount), data);
            }
        }

        public RowBufferIndex Build() => new(Builders.ToDictionary(b => b.Key, b => b.Value.Build()));

        private static bool IsValid(BufferRow row, ReadOnlySpan<BufferColumn> columns, int dataLength)
        {
            if ((ulong)row.FirstColumn + row.ColumnCount > (ulong)columns.Length)
            {
                return false;
            }

            foreach (var column in columns.Slice((int)row.FirstColumn, (int)row.ColumnCount))
            {
                if ((ulong)column.Data + column.Captured > (ulong)dataLength)
                {
                    return false;
                }
            }

            return true;
        }
    }

    private sealed record IteratorTimeline(ulong[] Ids, (RowBufferTable Table, int Index)[] Rows)
    {
        public static IteratorTimeline Build(List<(ulong Id, RowBufferTable Table, int Index)> captures)
        {
            captures.Sort((left, right) => left.Id.CompareTo(right.Id));

            return new IteratorTimeline([.. captures.Select(c => c.Id)], [.. captures.Select(c => (c.Table, c.Index))]);
        }
    }
}
