using System.Collections;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed class TraceCallList : IReadOnlyList<TraceArgumentCall>
{
    internal TraceCallList(ulong address,
                           ulong instance,
                           TraceCallStore.Chunk[] chunks,
                           TraceCallStore.ChunkCache columns,
                           LocatorTable? locators = null)
    {
        Address = address;
        Instance = instance;
        Chunks = chunks;
        Columns = columns;
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

    private TraceCallStore.ChunkCache Columns { get; }

    private LocatorTable? Locators { get; }

    public TraceArgumentCall this[int index]
    {
        get
        {
            var (columns, call) = GetLocation(index);

            return columns.GetCall(call, Locators?.GetLocators(index));
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

    public int IndexOfEnclosing(TracePosition position, uint thread)
    {
        for (var index = FirstStartingAfter(position) - 1; index >= 0; index--)
        {
            if (GetThread(index) == thread && GetToPosition(index) >= position)
            {
                return index;
            }
        }

        return -1;
    }

    public TracePosition GetFromPosition(int index)
    {
        var (columns, call) = GetLocation(index);

        return columns.GetFromPosition(call);
    }

    public TracePosition GetToPosition(int index)
    {
        var (columns, call) = GetLocation(index);

        return columns.GetToPosition(call);
    }

    public uint GetThread(int index)
    {
        var (columns, call) = GetLocation(index);

        return columns.GetThread(call);
    }

    public ulong? GetReturnValue(int index)
    {
        var (columns, call) = GetLocation(index);

        return columns.IsReturned(call) ? columns.GetReturnValue(call) : null;
    }

    internal int GetNodeIndex(int index)
    {
        var (columns, call) = GetLocation(index);

        return columns.GetNodeIndex(call);
    }

    private (TraceCallColumns Columns, int Call) GetLocation(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

        var chunk = Array.BinarySearch(Starts, index);

        chunk = chunk >= 0 ? chunk : ~chunk - 1;

        return (Columns.Get(Chunks[chunk]), index - Starts[chunk]);
    }

    private int FirstStartingAfter(TracePosition position)
    {
        var low = 0;

        var high = Count;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            if (GetFromPosition(middle) <= position)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}
