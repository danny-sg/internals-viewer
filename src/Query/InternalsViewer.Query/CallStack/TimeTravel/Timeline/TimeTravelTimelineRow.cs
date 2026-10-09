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

    public TimeTravelTimelineSpan Span(TimeTravelTimelineAxis axis, int index)
        => new(Starts(axis)[index],
               Ends(axis)[index],
               Nodes[index],
               CallAt(index),
               (TimeTravelSpanFlags)Flags[index]);

    public TimeTravelTimelineRow Where(Func<int, bool> include) => Keep(index => include(Nodes[index]));

    public int FirstEndingAfter(TimeTravelTimelineAxis axis, double x, int from = 0)
        => LowerBound(Ends(axis), x, from, inclusive: false);


    public int FirstStartingFrom(TimeTravelTimelineAxis axis, double x, int from) => LowerBound(Starts(axis), x, from, inclusive: true);

    public int IndexAt(TimeTravelTimelineAxis axis, double x, double tolerance)
    {
        var index = FirstEndingAfter(axis, x - tolerance);

        return index < Count && Starts(axis)[index] <= x + tolerance ? index : -1;
    }

    private TimeTravelTimelineRow Keep(Func<int, bool> keep)
    {
        var kept = new List<int>(Count);

        for (var index = 0; index < Count; index++)
        {
            if (keep(index))
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

    private static int LowerBound(ReadOnlySpan<double> values, double x, int from, bool inclusive)
    {
        var low = from;

        var high = values.Length;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            if (inclusive ? values[middle] < x : values[middle] <= x)
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
