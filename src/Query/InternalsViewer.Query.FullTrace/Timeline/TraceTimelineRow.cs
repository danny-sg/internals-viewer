using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceTimelineRow
{
    internal TraceTimelineRow(TraceScale scale, TraceThreadClock clock, TraceSpanStore store, int offset, int count)
        : this(scale, clock, store, null, offset, count)
    {
    }

    internal TraceTimelineRow(TraceScale scale, TraceThreadClock clock, TraceSpanStore store, int[] indexes)
        : this(scale, clock, store, indexes, 0, indexes.Length)
    {
    }

    private TraceTimelineRow(TraceScale scale,
                             TraceThreadClock clock,
                             TraceSpanStore store,
                             int[]? indexes,
                             int offset,
                             int count)
    {
        Scale = scale;
        Clock = clock;
        Store = store;
        Indexes = indexes;
        Offset = offset;
        Count = count;
    }

    public int Count { get; private set; }

    public TraceScale Scale { get; }

    internal long Bytes => Indexes is null ? 0 : Indexes.Length * (long)sizeof(int);

    private TraceThreadClock Clock { get; }

    private TraceSpanStore Store { get; }

    private int[]? Indexes { get; }

    private int Offset { get; }

    public TracePosition GetFromPosition(int index) => Store.Froms[GetStoreIndex(index)];

    public TracePosition GetToPosition(int index) => Store.Tos[GetStoreIndex(index)];

    public TracePoint GetFrom(int index) => GetPoint(GetFromPosition(index));

    public TracePoint GetTo(int index) => GetPoint(GetToPosition(index));

    public double GetStart(TraceTimelineAxis axis, int index) => Scale.GetValue(GetFromPosition(index), Clock, axis);

    public double GetEnd(TraceTimelineAxis axis, int index) => Scale.GetValue(GetToPosition(index), Clock, axis);

    public int GetCallNodeIndex(int index) => Store.CallNodeIndexes[GetStoreIndex(index)];

    public int GetCallIndex(int index)
        => Store.Calls[GetStoreIndex(index)] is var callIndex && callIndex == uint.MaxValue ? -1 : (int)callIndex;

    public TraceSpanFlags FlagsAt(int index) => (TraceSpanFlags)Store.Flags[GetStoreIndex(index)];

    public TraceSpan GetSpan(int index) => new(GetFrom(index), GetTo(index), GetCallNodeIndex(index), GetCallIndex(index), FlagsAt(index));

    public TraceTimelineRow Where(Func<int, bool> include)
    {
        var indexes = new int[Count];

        var kept = 0;

        for (var index = 0; index < Count; index++)
        {
            var storeIndex = GetStoreIndex(index);

            if (include(Store.CallNodeIndexes[storeIndex]))
            {
                indexes[kept++] = storeIndex;
            }
        }

        if (kept == Count)
        {
            return this;
        }

        Array.Resize(ref indexes, kept);

        return new TraceTimelineRow(Scale, Clock, Store, indexes);
    }

    internal int RemoveAll(Func<int, bool> isRemoved)
    {
        var kept = 0;

        for (var index = 0; index < Count; index++)
        {
            var storeIndex = GetStoreIndex(index);

            if (isRemoved(Store.CallNodeIndexes[storeIndex]))
            {
                continue;
            }

            if (Indexes is null)
            {
                Store.Move(storeIndex, Offset + kept);
            }
            else
            {
                Indexes[Offset + kept] = storeIndex;
            }

            kept++;
        }

        var removed = Count - kept;

        Count = kept;

        return removed;
    }

    public int GetFirstEndingAfter(TraceTimelineAxis axis, double value, int from = 0)
        => GetLowerBound(Store.Tos, axis, value, from, strict: true);

    public int GetFirstStartingFrom(TraceTimelineAxis axis, double value, int from = 0)
        => GetLowerBound(Store.Froms, axis, value, from, strict: false);

    public int GetFirstStartingAfter(TraceTimelineAxis axis, double value) => GetLowerBound(Store.Froms, axis, value, 0, strict: true);

    public int GetIndex(TraceTimelineAxis axis, double value, double tolerance)
    {
        var index = GetFirstEndingAfter(axis, value - tolerance);

        return index < Count && GetStart(axis, index) <= value + tolerance ? index : -1;
    }

    public int GetIndex(TracePosition position)
    {
        var index = GetLowerBound(Store.Tos, position, strict: true);

        return index < Count && GetFromPosition(index) <= position ? index : -1;
    }

    internal int GetIndex(TracePosition from, int callNodeIndex, int callIndex)
    {
        var index = GetLowerBound(Store.Froms, from, strict: false);

        return index < Count
               && GetFromPosition(index) == from
               && GetCallNodeIndex(index) == callNodeIndex
               && GetCallIndex(index) == callIndex
            ? index
            : -1;
    }

    internal int GetStoreIndex(int index) => Indexes is null ? Offset + index : Indexes[Offset + index];

    internal TraceTimelineRow Slice(int start, int count)
        => start == 0 && count == Count
            ? this
            : new TraceTimelineRow(Scale, Clock, Store, Indexes, Offset + start, count);

    private int GetLowerBound(TracePosition[] positions, TraceTimelineAxis axis, double value, int from, bool strict)
    {
        var low = from;

        var high = Count;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            var probe = Scale.GetValue(positions[GetStoreIndex(middle)], Clock, axis);

            if (strict ? probe <= value : probe < value)
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

    private int GetLowerBound(TracePosition[] positions, TracePosition position, bool strict)
    {
        var low = 0;

        var high = Count;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            var probe = positions[GetStoreIndex(middle)];

            if (strict ? probe <= position : probe < position)
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

    private TracePoint GetPoint(TracePosition position) => new(position, Clock.GetInstructions(position));
}
