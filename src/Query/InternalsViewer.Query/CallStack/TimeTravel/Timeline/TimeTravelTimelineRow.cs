namespace InternalsViewer.Query.CallStack.TimeTravel.Timeline;

public sealed class TimeTravelTimelineRow
{
    internal TimeTravelTimelineRow(double[] positionStarts,
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

    public ReadOnlySpan<double> Starts(TimeTravelTimelineAxis axis)
        => axis == TimeTravelTimelineAxis.Position ? PositionStarts : InstructionStarts;

    public ReadOnlySpan<double> Ends(TimeTravelTimelineAxis axis)
        => axis == TimeTravelTimelineAxis.Position ? PositionEnds : InstructionEnds;

    public int NodeAt(int index) => Nodes[index];

    public int CallAt(int index) => Calls[index] == uint.MaxValue ? -1 : (int)Calls[index];

    public TimeTravelSpanFlags FlagsAt(int index) => (TimeTravelSpanFlags)Flags[index];

    public TimeTravelTimelineSpan Span(TimeTravelTimelineAxis axis, int index)
        => new(Starts(axis)[index], Ends(axis)[index], Nodes[index], CallAt(index), FlagsAt(index));

    public TimeTravelTimelineRow Where(Func<int, bool> include)
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

        return new TimeTravelTimelineRow([.. kept.Select(i => PositionStarts[i])],
                                         [.. kept.Select(i => PositionEnds[i])],
                                         [.. kept.Select(i => InstructionStarts[i])],
                                         [.. kept.Select(i => InstructionEnds[i])],
                                         [.. kept.Select(i => Nodes[i])],
                                         [.. kept.Select(i => Calls[i])],
                                         [.. kept.Select(i => Flags[i])]);
    }

    public int FirstEndingAfter(TimeTravelTimelineAxis axis, double x, int from = 0) => SortedSearch.FirstAfter(Ends(axis), x, from);

    public int FirstStartingFrom(TimeTravelTimelineAxis axis, double x, int from) => SortedSearch.FirstAtOrAfter(Starts(axis), x, from);

    public int FirstStartingAfter(TimeTravelTimelineAxis axis, double x) => SortedSearch.FirstAfter(Starts(axis), x);

    public int IndexAt(TimeTravelTimelineAxis axis, double x, double tolerance)
    {
        var index = FirstEndingAfter(axis, x - tolerance);

        return index < Count && Starts(axis)[index] <= x + tolerance ? index : -1;
    }
}
