using System.Collections;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed class TraceCallList : IReadOnlyList<TraceArgumentCall>
{
    private const int CachedChunks = 2;

    internal TraceCallList(ulong address,
                           ulong instance,
                           TraceCallStore.Chunk[] chunks,
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

    private TraceCallStore.Chunk[] Chunks { get; }

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

            return TraceArgumentCall.Decode(row, Locators?.GetLocators(index));
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

    public int IndexOf(ulong id)
    {
        var low = 0;

        var high = Count - 1;

        while (low <= high)
        {
            var middle = low + (high - low) / 2;

            var current = GetId(middle);

            if (current == id)
            {
                return middle;
            }

            if (current < id)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return -1;
    }

    public int IndexOfEnclosing(ulong id)
    {
        var low = 0;

        var high = Count - 1;

        var latest = -1;

        while (low <= high)
        {
            var middle = low + (high - low) / 2;

            if (GetId(middle) <= id)
            {
                latest = middle;

                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        for (var index = latest; index >= 0; index--)
        {
            var end = GetEndId(index);

            if (end == 0 || end >= id)
            {
                return index;
            }
        }

        return -1;
    }

    public ulong GetId(int index)
    {
        var (columns, calls, call) = GetLocation(index);

        return columns[TraceArgumentCall.IdIndex * calls + call];
    }

    internal int GetNodeIndex(int index)
    {
        var (columns, calls, call) = GetLocation(index);

        return (int)columns[TraceArgumentCall.NodeIndex * calls + call];
    }

    internal ulong GetEndId(int index)
    {
        var (columns, calls, call) = GetLocation(index);

        return columns[TraceArgumentCall.EndIdIndex * calls + call];
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

            var decompressed = TraceCallStore.Decompress(Chunks[chunk]);

            Cache.Insert(0, (chunk, decompressed));

            if (Cache.Count > CachedChunks)
            {
                Cache.RemoveAt(Cache.Count - 1);
            }

            return decompressed;
        }
    }
}
