using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.RowBuffers;

public sealed partial class RowBufferIndex
{
    internal RowBufferIndex(IEnumerable<RowBuffer> rows)
    {
        Rows = rows.GroupBy(r => (r.Function, r.Instance)).ToDictionary(g => g.Key, g => g.OrderBy(r => r.Call).ToArray());

        Count = Rows.Values.Sum(r => r.Length);
    }

    public static RowBufferIndex Empty { get; } = new([]);

    public int Count { get; }

    private Dictionary<(ulong Function, ulong Instance), RowBuffer[]> Rows { get; }

    private Dictionary<ulong, RowBuffer[]> Iterators
        => field ??= Rows.Values
                         .SelectMany(r => r)
                         .GroupBy(r => r.Iterator)
                         .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Id).ToArray());

    public bool HasRows(ulong address, ulong instance) => Rows.ContainsKey((address, instance));

    public bool HasRow(ulong address, ulong instance, int call) => GetRow(address, instance, call) is not null;

    public IReadOnlyList<RowBuffer> GetRows(ulong address, ulong instance) => Rows.GetValueOrDefault((address, instance)) ?? [];

    public RowBuffer? GetRow(ulong address, ulong instance, int call)
    {
        var rows = GetRows(address, instance);

        var index = SortedSearch.FirstAtOrAfter(rows, call, r => r.Call);

        return index < rows.Count && rows[index].Call == call ? rows[index] : null;
    }

    public int GetColumnCount(ulong address, ulong instance)
        => GetRows(address, instance).Select(r => r.Columns.Count).DefaultIfEmpty().Max();

    public RowBuffer? GetRowAt(ulong iterator, ulong callId)
    {
        if (!Iterators.TryGetValue(iterator, out var rows))
        {
            return null;
        }

        var index = SortedSearch.FirstAtOrAfter(rows, callId, r => r.Id) - 1;

        return index < 0 ? null : rows[index];
    }

    internal void AnnotateNodes(TraceCallStore store)
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
}
