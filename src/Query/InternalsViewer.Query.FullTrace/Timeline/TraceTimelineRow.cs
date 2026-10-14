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
    {
        PositionStarts = positionStarts;
        PositionEnds = positionEnds;
        InstructionStarts = instructionStarts;
        InstructionEnds = instructionEnds;
        Nodes = nodes;
        Calls = calls;
        Flags = flags;
    }

    public int Count => Nodes.Length;

    private double[] PositionStarts { get; }

    private double[] PositionEnds { get; }

    private double[] InstructionStarts { get; }

    private double[] InstructionEnds { get; }

    private int[] Nodes { get; }

    private uint[] Calls { get; }

    private byte[] Flags { get; }

    public ReadOnlySpan<double> Starts(TraceTimelineAxis axis)
        => axis == TraceTimelineAxis.Position ? PositionStarts : InstructionStarts;

    public ReadOnlySpan<double> Ends(TraceTimelineAxis axis)
        => axis == TraceTimelineAxis.Position ? PositionEnds : InstructionEnds;

    public int NodeAt(int index) => Nodes[index];

    public int CallAt(int index) => Calls[index] == uint.MaxValue ? -1 : (int)Calls[index];

    public TraceSpanFlags FlagsAt(int index) => (TraceSpanFlags)Flags[index];

    public TraceSpan Span(TraceTimelineAxis axis, int index)
        => new(Starts(axis)[index], Ends(axis)[index], Nodes[index], CallAt(index), FlagsAt(index));

    public TraceTimelineRow Where(Func<int, bool> include)
    {
        var kept = new List<int>(Count);

        for (var index = 0; index < Count; index++)
        {
            if (include(Nodes[index]))
            {
                kept.Add(index);
            }
        }

        if (kept.Count == Count)
        {
            return this;
        }

        return new TraceTimelineRow([.. kept.Select(i => PositionStarts[i])],
                                    [.. kept.Select(i => PositionEnds[i])],
                                    [.. kept.Select(i => InstructionStarts[i])],
                                    [.. kept.Select(i => InstructionEnds[i])],
                                    [.. kept.Select(i => Nodes[i])],
                                    [.. kept.Select(i => Calls[i])],
                                    [.. kept.Select(i => Flags[i])]);
    }

    public int FirstEndingAfter(TraceTimelineAxis axis, double x, int from = 0) => SortedSearch.FirstAfter(Ends(axis), x, from);

    public int FirstStartingFrom(TraceTimelineAxis axis, double x, int from) => SortedSearch.FirstAtOrAfter(Starts(axis), x, from);

    public int FirstStartingAfter(TraceTimelineAxis axis, double x) => SortedSearch.FirstAfter(Starts(axis), x);

    public int IndexAt(TraceTimelineAxis axis, double x, double tolerance)
    {
        var index = FirstEndingAfter(axis, x - tolerance);

        return index < Count && Starts(axis)[index] <= x + tolerance ? index : -1;
    }
}
