using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceTimelineRow
{
    internal TraceTimelineRow(TraceScale scale,
                              TraceThreadClock clock,
                              TracePosition[] fromPositions,
                              TracePosition[] toPositions,
                              int[] callNodeIndexes,
                              uint[] calls,
                              byte[] flags)
        : this(scale, clock, fromPositions, toPositions, callNodeIndexes, calls, flags, 0, callNodeIndexes.Length)
    {
    }

    private TraceTimelineRow(TraceScale scale,
                             TraceThreadClock clock,
                             TracePosition[] fromPositions,
                             TracePosition[] toPositions,
                             int[] callNodeIndexes,
                             uint[] calls,
                             byte[] flags,
                             int offset,
                             int count)
    {
        Scale = scale;
        Clock = clock;
        FromPositions = fromPositions;
        ToPositions = toPositions;
        CallNodeIndexes = callNodeIndexes;
        Calls = calls;
        Flags = flags;
        Offset = offset;
        Count = count;
    }

    public int Count { get; private set; }

    public TraceScale Scale { get; }

    internal long Bytes => FromPositions.Length * 2L * sizeof(ulong)
                           + CallNodeIndexes.Length * (long)sizeof(int)
                           + Calls.Length * (long)sizeof(uint)
                           + Flags.Length;

    private TraceThreadClock Clock { get; }

    private int Offset { get; }

    private TracePosition[] FromPositions { get; }

    private TracePosition[] ToPositions { get; }

    private int[] CallNodeIndexes { get; }

    private uint[] Calls { get; }

    private byte[] Flags { get; }

    internal ReadOnlySpan<TracePosition> Froms => FromPositions.AsSpan(Offset, Count);

    private ReadOnlySpan<TracePosition> Tos => ToPositions.AsSpan(Offset, Count);

    public TracePosition GetFromPosition(int index) => FromPositions[Offset + index];

    public TracePosition GetToPosition(int index) => ToPositions[Offset + index];

    public TracePoint GetFrom(int index) => GetPoint(GetFromPosition(index));

    public TracePoint GetTo(int index) => GetPoint(GetToPosition(index));

    public double GetStart(TraceTimelineAxis axis, int index) => Scale.GetValue(GetFromPosition(index), Clock, axis);

    public double GetEnd(TraceTimelineAxis axis, int index) => Scale.GetValue(GetToPosition(index), Clock, axis);

    public int GetCallNodeIndex(int index) => CallNodeIndexes[Offset + index];

    public int GetCallIndex(int index) => Calls[Offset + index] is var callIndex && callIndex == uint.MaxValue ? -1 : (int)callIndex;

    public TraceSpanFlags FlagsAt(int index) => (TraceSpanFlags)Flags[Offset + index];

    public TraceSpan GetSpan(int index) => new(GetFrom(index), GetTo(index), GetCallNodeIndex(index), GetCallIndex(index), FlagsAt(index));

    public TraceTimelineRow Where(Func<int, bool> include)
    {
        var included = new bool[Count];

        var kept = 0;

        for (var index = 0; index < Count; index++)
        {
            included[index] = include(GetCallNodeIndex(index));

            kept += included[index] ? 1 : 0;
        }

        if (kept == Count)
        {
            return this;
        }

        var froms = new TracePosition[kept];

        var tos = new TracePosition[kept];

        var callNodeIndexes = new int[kept];

        var calls = new uint[kept];

        var flags = new byte[kept];

        var at = 0;

        for (var index = 0; index < Count; index++)
        {
            if (!included[index])
            {
                continue;
            }

            froms[at] = FromPositions[Offset + index];
            tos[at] = ToPositions[Offset + index];
            callNodeIndexes[at] = CallNodeIndexes[Offset + index];
            calls[at] = Calls[Offset + index];
            flags[at] = Flags[Offset + index];

            at++;
        }

        return new TraceTimelineRow(Scale, Clock, froms, tos, callNodeIndexes, calls, flags);
    }

    internal int RemoveAll(Func<int, bool> isRemoved)
    {
        var kept = 0;

        for (var index = Offset; index < Offset + Count; index++)
        {
            if (isRemoved(CallNodeIndexes[index]))
            {
                continue;
            }

            var at = Offset + kept;

            FromPositions[at] = FromPositions[index];
            ToPositions[at] = ToPositions[index];
            CallNodeIndexes[at] = CallNodeIndexes[index];
            Calls[at] = Calls[index];
            Flags[at] = Flags[index];

            kept++;
        }

        var removed = Count - kept;

        Count = kept;

        return removed;
    }

    public int GetFirstEndingAfter(TraceTimelineAxis axis, double value, int from = 0)
        => Scale.GetFirstAfter(Tos, Clock, axis, value, from);

    public int GetFirstStartingFrom(TraceTimelineAxis axis, double value, int from = 0)
        => Scale.GetFirstAtOrAfter(Froms, Clock, axis, value, from);

    public int GetFirstStartingAfter(TraceTimelineAxis axis, double value) => Scale.GetFirstAfter(Froms, Clock, axis, value);

    public int GetIndex(TraceTimelineAxis axis, double value, double tolerance)
    {
        var index = GetFirstEndingAfter(axis, value - tolerance);

        return index < Count && GetStart(axis, index) <= value + tolerance ? index : -1;
    }

    public int GetIndex(TracePosition position)
    {
        var index = SortedSearch.FirstAfter(Tos, position);

        return index < Count && GetFromPosition(index) <= position ? index : -1;
    }

    internal int GetIndex(TracePosition from, int callNodeIndex, int callIndex)
    {
        var index = SortedSearch.FirstAtOrAfter(Froms, from);

        return index < Count
               && GetFromPosition(index) == from
               && GetCallNodeIndex(index) == callNodeIndex
               && GetCallIndex(index) == callIndex
            ? index
            : -1;
    }

    internal TraceTimelineRow Slice(int start, int count)
        => start == 0 && count == Count
            ? this
            : new TraceTimelineRow(Scale, Clock, FromPositions, ToPositions, CallNodeIndexes, Calls, Flags, Offset + start, count);

    private TracePoint GetPoint(TracePosition position) => new(position, Clock.GetInstructions(position));
}
