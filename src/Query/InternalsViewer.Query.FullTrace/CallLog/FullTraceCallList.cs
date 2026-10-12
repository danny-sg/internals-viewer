using System.Collections;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.CallLog;

public sealed class FullTraceCallList : IReadOnlyList<FullTraceArgumentCall>
{
    private const int CachedChunks = 2;

    internal FullTraceCallList(ulong address,
                               ulong instance,
                               FullTraceCallLog.Chunk[] chunks,
                               LocatorTable? locators = null)
    {
        Address = address;
        Instance = instance;
        Chunks = chunks;
        Locators = locators;
        Starts = new int[chunks.Length];

        var start = 0;

        for (var index = 0; index < chunks.Length; index++)
        {
            Starts[index] = start;

            start += chunks[index].Calls;
        }

        Count = start;
    }

    public ulong Address { get; }

    public ulong Instance { get; }

    public int Count { get; }

    private FullTraceCallLog.Chunk[] Chunks { get; }

    private int[] Starts { get; }

    private LocatorTable? Locators { get; }

    private List<(int Chunk, ulong[] Columns)> Cache { get; } = [];

    private Lock Sync { get; } = new();

    public FullTraceArgumentCall this[int index]
    {
        get
        {
            var (columns, calls, call) = Locate(index);

            var row = new ulong[FullTraceArgumentCall.ValueCount];

            for (var column = 0; column < row.Length; column++)
            {
                row[column] = columns[column * calls + call];
            }

            return FullTraceArgumentCall.From(row, Locators?.Of(index));
        }
    }

    public IEnumerator<FullTraceArgumentCall> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal int NodeIndexAt(int index)
    {
        var (columns, calls, call) = Locate(index);

        return (int)columns[(FullTraceArgumentCall.ValueCount - 1) * calls + call];
    }

    private (ulong[] Columns, int Calls, int Call) Locate(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

        var chunk = Array.BinarySearch(Starts, index);

        chunk = chunk >= 0 ? chunk : ~chunk - 1;

        return (ColumnsOf(chunk), Chunks[chunk].Calls, index - Starts[chunk]);
    }

    private ulong[] ColumnsOf(int chunk)
    {
        lock (Sync)
        {
            foreach (var (cached, columns) in Cache)
            {
                if (cached == chunk)
                {
                    return columns;
                }
            }

            var decompressed = FullTraceCallLog.Decompress(Chunks[chunk]);

            Cache.Insert(0, (chunk, decompressed));

            if (Cache.Count > CachedChunks)
            {
                Cache.RemoveAt(Cache.Count - 1);
            }

            return decompressed;
        }
    }
}
