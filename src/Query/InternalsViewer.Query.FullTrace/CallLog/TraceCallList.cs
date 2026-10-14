using System.Collections;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.CallLog;

public sealed class TraceCallList : IReadOnlyList<TraceArgumentCall>
{
    private const int CachedChunks = 2;

    internal TraceCallList(ulong address,
                           ulong instance,
                           TraceCallLog.Chunk[] chunks,
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

    private TraceCallLog.Chunk[] Chunks { get; }

    private int[] Starts { get; }

    private LocatorTable? Locators { get; }

    private List<(int Chunk, ulong[] Columns)> Cache { get; } = [];

    private Lock Sync { get; } = new();

    public TraceArgumentCall this[int index]
    {
        get
        {
            var (columns, calls, call) = GetLocation(index);

            var row = new ulong[TraceArgumentCall.ValueCount];

            for (var column = 0; column < row.Length; column++)
            {
                row[column] = columns[column * calls + call];
            }

            return TraceArgumentCall.Decode(row, Locators?.LocatorsOf(index), index);
        }
    }

    public IEnumerator<TraceArgumentCall> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal int NodeIndexAt(int index)
    {
        var (columns, calls, call) = GetLocation(index);

        return (int)columns[(TraceArgumentCall.ValueCount - 1) * calls + call];
    }

    private (ulong[] Columns, int Calls, int Call) GetLocation(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

        var chunk = Array.BinarySearch(Starts, index);

        chunk = chunk >= 0 ? chunk : ~chunk - 1;

        return (GetChunkColumns(chunk), Chunks[chunk].Calls, index - Starts[chunk]);
    }

    private ulong[] GetChunkColumns(int chunk)
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

            var decompressed = TraceCallLog.Decompress(Chunks[chunk]);

            Cache.Insert(0, (chunk, decompressed));

            if (Cache.Count > CachedChunks)
            {
                Cache.RemoveAt(Cache.Count - 1);
            }

            return decompressed;
        }
    }
}
