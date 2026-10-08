using System.Collections;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed class TimeTravelCallList : IReadOnlyList<TimeTravelArgumentCall>
{
    private const int CachedChunks = 2;

    internal TimeTravelCallList(ulong address, ulong instance, TimeTravelCallLog.Chunk[] chunks)
    {
        Address = address;
        Instance = instance;
        Chunks = chunks;
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

    private TimeTravelCallLog.Chunk[] Chunks { get; }

    private int[] Starts { get; }

    private List<(int Chunk, ulong[] Columns)> Cache { get; } = [];

    private Lock Sync { get; } = new();

    public TimeTravelArgumentCall this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

            var chunk = Array.BinarySearch(Starts, index);

            chunk = chunk >= 0 ? chunk : ~chunk - 1;

            var columns = ColumnsOf(chunk);

            var calls = Chunks[chunk].Calls;

            var call = index - Starts[chunk];

            var row = new ulong[TimeTravelArgumentCall.ValueCount];

            for (var column = 0; column < row.Length; column++)
            {
                row[column] = columns[column * calls + call];
            }

            return TimeTravelArgumentCall.From(row);
        }
    }

    public IEnumerator<TimeTravelArgumentCall> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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

            var decompressed = TimeTravelCallLog.Decompress(Chunks[chunk]);

            Cache.Insert(0, (chunk, decompressed));

            if (Cache.Count > CachedChunks)
            {
                Cache.RemoveAt(Cache.Count - 1);
            }

            return decompressed;
        }
    }
}
