using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceTimelineRow
{
    internal TraceTimelineRow(double[] positionStarts,
                              double[] positionEnds,
                              double[] instructionStarts,
                              double[] instructionEnds,
                              int[] nodes,
                              uint[] calls,
                              byte[] flags)
        : this(positionStarts, positionEnds, instructionStarts, instructionEnds, nodes, calls, flags, 0, nodes.Length)
    {
    }

    private TraceTimelineRow(double[] positionStarts,
                             double[] positionEnds,
                             double[] instructionStarts,
                             double[] instructionEnds,
                             int[] nodes,
                             uint[] calls,
                             byte[] flags,
                             int offset,
                             int count)
    {
        PositionStarts = positionStarts;
        PositionEnds = positionEnds;
        InstructionStarts = instructionStarts;
        InstructionEnds = instructionEnds;
        Nodes = nodes;
        Calls = calls;
        Flags = flags;
        Offset = offset;
        Count = count;
    }

    public int Count { get; }

    private int Offset { get; }

    private double[] PositionStarts { get; }

    private double[] PositionEnds { get; }

    private double[] InstructionStarts { get; }

    private double[] InstructionEnds { get; }

    private int[] Nodes { get; }

    private uint[] Calls { get; }

    private byte[] Flags { get; }

    public ReadOnlySpan<double> Starts(TraceTimelineAxis axis)
        => (axis == TraceTimelineAxis.Position ? PositionStarts : InstructionStarts).AsSpan(Offset, Count);

    public ReadOnlySpan<double> Ends(TraceTimelineAxis axis)
        => (axis == TraceTimelineAxis.Position ? PositionEnds : InstructionEnds).AsSpan(Offset, Count);

    public int NodeAt(int index) => Nodes[Offset + index];

    public int CallAt(int index) => Calls[Offset + index] is var call && call == uint.MaxValue ? -1 : (int)call;

    public TraceSpanFlags FlagsAt(int index) => (TraceSpanFlags)Flags[Offset + index];

    public TraceSpan Span(TraceTimelineAxis axis, int index)
        => new(Starts(axis)[index], Ends(axis)[index], NodeAt(index), CallAt(index), FlagsAt(index));

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

        return new TraceTimelineRow([.. kept.Select(i => PositionStarts[Offset + i])],
                                    [.. kept.Select(i => PositionEnds[Offset + i])],
                                    [.. kept.Select(i => InstructionStarts[Offset + i])],
                                    [.. kept.Select(i => InstructionEnds[Offset + i])],
                                    [.. kept.Select(i => Nodes[Offset + i])],
                                    [.. kept.Select(i => Calls[Offset + i])],
                                    [.. kept.Select(i => Flags[Offset + i])]);
    }

    public int FirstEndingAfter(TraceTimelineAxis axis, double x, int from = 0) => SortedSearch.FirstAfter(Ends(axis), x, from);

    public int FirstStartingFrom(TraceTimelineAxis axis, double x, int from) => SortedSearch.FirstAtOrAfter(Starts(axis), x, from);

    public int FirstStartingAfter(TraceTimelineAxis axis, double x) => SortedSearch.FirstAfter(Starts(axis), x);

    public int IndexAt(TraceTimelineAxis axis, double x, double tolerance)
    {
        var index = FirstEndingAfter(axis, x - tolerance);

        return index < Count && Starts(axis)[index] <= x + tolerance ? index : -1;
    }

    internal TraceTimelineRow Slice(int start, int count)
        => start == 0 && count == Count
            ? this
            : new TraceTimelineRow(PositionStarts,
                                   PositionEnds,
                                   InstructionStarts,
                                   InstructionEnds,
                                   Nodes,
                                   Calls,
                                   Flags,
                                   Offset + start,
                                   count);
}
