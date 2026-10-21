using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceTimelineRow
{
    internal TraceTimelineRow(TraceScale scale, TracePoint[] fromPoints, TracePoint[] toPoints, int[] nodes, uint[] calls, byte[] flags)
        : this(scale, fromPoints, toPoints, nodes, calls, flags, 0, nodes.Length)
    {
    }

    private TraceTimelineRow(TraceScale scale,
                             TracePoint[] fromPoints,
                             TracePoint[] toPoints,
                             int[] nodes,
                             uint[] calls,
                             byte[] flags,
                             int offset,
                             int count)
    {
        Scale = scale;
        FromPoints = fromPoints;
        ToPoints = toPoints;
        Nodes = nodes;
        Calls = calls;
        Flags = flags;
        Offset = offset;
        Count = count;
    }

    public int Count { get; }

    public TraceScale Scale { get; }

    private int Offset { get; }

    private TracePoint[] FromPoints { get; }

    private TracePoint[] ToPoints { get; }

    private int[] Nodes { get; }

    private uint[] Calls { get; }

    private byte[] Flags { get; }

    internal ReadOnlySpan<TracePoint> Froms => FromPoints.AsSpan(Offset, Count);

    private ReadOnlySpan<TracePoint> Tos => ToPoints.AsSpan(Offset, Count);

    public TracePoint GetFrom(int index) => FromPoints[Offset + index];

    public TracePoint GetTo(int index) => ToPoints[Offset + index];

    public double GetStart(TraceTimelineAxis axis, int index) => Scale.GetValue(GetFrom(index), axis);

    public double GetEnd(TraceTimelineAxis axis, int index) => Scale.GetValue(GetTo(index), axis);

    public int NodeAt(int index) => Nodes[Offset + index];

    public int CallAt(int index) => Calls[Offset + index] is var call && call == uint.MaxValue ? -1 : (int)call;

    public TraceSpanFlags FlagsAt(int index) => (TraceSpanFlags)Flags[Offset + index];

    public TraceSpan GetSpan(int index) => new(GetFrom(index), GetTo(index), NodeAt(index), CallAt(index), FlagsAt(index));

    public TraceTimelineRow Where(Func<int, bool> include)
    {
        var kept = new List<int>(Count);

        for (var index = 0; index < Count; index++)
        {
            if (include(NodeAt(index)))
            {
                kept.Add(index);
            }
        }

        if (kept.Count == Count)
        {
            return this;
        }

        return new TraceTimelineRow(Scale,
                                    [.. kept.Select(i => FromPoints[Offset + i])],
                                    [.. kept.Select(i => ToPoints[Offset + i])],
                                    [.. kept.Select(i => Nodes[Offset + i])],
                                    [.. kept.Select(i => Calls[Offset + i])],
                                    [.. kept.Select(i => Flags[Offset + i])]);
    }

    public int GetFirstEndingAfter(TraceTimelineAxis axis, double value, int from = 0) => Scale.GetFirstAfter(Tos, axis, value, from);

    public int GetFirstStartingFrom(TraceTimelineAxis axis, double value, int from = 0)
        => Scale.GetFirstAtOrAfter(Froms, axis, value, from);

    public int GetFirstStartingAfter(TraceTimelineAxis axis, double value) => Scale.GetFirstAfter(Froms, axis, value);

    public int GetIndex(TraceTimelineAxis axis, double value, double tolerance)
    {
        var index = GetFirstEndingAfter(axis, value - tolerance);

        return index < Count && GetStart(axis, index) <= value + tolerance ? index : -1;
    }

    public int GetIndex(TracePosition position)
    {
        var index = SortedSearch.FirstAfter(Tos, position, p => p.Position);

        return index < Count && GetFrom(index).Position <= position ? index : -1;
    }

    internal int GetIndex(TracePosition from, int node, int call)
    {
        var index = SortedSearch.FirstAtOrAfter(Froms, from, p => p.Position);

        return index < Count && GetFrom(index).Position == from && NodeAt(index) == node && CallAt(index) == call ? index : -1;
    }

    internal TraceTimelineRow Slice(int start, int count)
        => start == 0 && count == Count
            ? this
            : new TraceTimelineRow(Scale, FromPoints, ToPoints, Nodes, Calls, Flags, Offset + start, count);
}
