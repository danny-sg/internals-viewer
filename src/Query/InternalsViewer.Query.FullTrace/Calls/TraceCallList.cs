using System.Collections;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed class TraceCallList : IReadOnlyList<TraceArgumentCall>
{
    internal TraceCallList(ulong functionAddress,
                           ulong instanceAddress,
                           TraceCallStore.Chunk[] chunks,
                           TraceCallStore.ChunkCache columns,
                           LocatorTable? locators = null)
    {
        FunctionAddress = functionAddress;
        InstanceAddress = instanceAddress;
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

    public ulong FunctionAddress { get; }

    public ulong InstanceAddress { get; }

    public int Count { get; }

    private TraceCallStore.Chunk[] Chunks { get; }

    private int[] Starts { get; }

    private TraceCallStore.ChunkCache Columns { get; }

    private LocatorTable? Locators { get; }

    public TraceArgumentCall this[int index]
    {
        get
        {
            var (chunk, callIndex) = GetLocation(index);

            return Columns.Read(chunk,
                                callIndex,
                                Locators?.GetLocators(index),
                                static (columns, offset, locators) => columns.GetCall(offset, locators));
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
        var (chunk, callIndex) = GetLocation(index);

        return Columns.Read(chunk, callIndex, static (columns, offset) => columns.GetFromPosition(offset));
    }

    public TracePosition GetToPosition(int index)
    {
        var (chunk, callIndex) = GetLocation(index);

        return Columns.Read(chunk, callIndex, static (columns, offset) => columns.GetToPosition(offset));
    }

    public uint GetThread(int index)
    {
        var (chunk, callIndex) = GetLocation(index);

        return Columns.Read(chunk, callIndex, static (columns, offset) => columns.GetThread(offset));
    }

    public ulong? GetReturnValue(int index)
    {
        var (chunk, callIndex) = GetLocation(index);

        return Columns.Read(chunk,
                            callIndex,
                            static (columns, offset) => columns.IsReturned(offset) ? columns.GetReturnValue(offset) : (ulong?)null);
    }

    internal int GetCallNodeIndex(int index)
    {
        var (chunk, callIndex) = GetLocation(index);

        return Columns.Read(chunk, callIndex, static (columns, offset) => columns.GetCallNodeIndex(offset));
    }

    internal IEnumerable<(TraceCallColumns Columns, int First)> ReadChunks(TraceCallStore.ColumnBuffer buffer)
    {
        for (var chunk = 0; chunk < Chunks.Length; chunk++)
        {
            yield return (buffer.Decompress(Chunks[chunk]), Starts[chunk]);
        }
    }

    private (TraceCallStore.Chunk Chunk, int CallIndex) GetLocation(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

        var chunk = Array.BinarySearch(Starts, index);

        chunk = chunk >= 0 ? chunk : ~chunk - 1;

        return (Chunks[chunk], index - Starts[chunk]);
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
