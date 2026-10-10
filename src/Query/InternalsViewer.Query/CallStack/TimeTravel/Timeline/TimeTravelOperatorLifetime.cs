using InternalsViewer.Query.CallStack.TimeTravel.Memory;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.CallStack.TimeTravel.Timeline;

public sealed record TimeTravelOperatorLifetime(uint Thread,
                                                ExecutionOperatorEvent Operator,
                                                int Node,
                                                IReadOnlyList<TimeTravelTimelineSpan> PositionCalls,
                                                IReadOnlyList<TimeTravelTimelineSpan> InstructionCalls,
                                                TimeTravelInUseCurve InUse)
{
    private double[] PositionEnds { get; } = [.. PositionCalls.Select(c => c.End)];

    private double[] InstructionStarts { get; } = [.. InstructionCalls.Select(c => c.Start)];

    private double[] InstructionEnds { get; } = [.. InstructionCalls.Select(c => c.End)];

    public IReadOnlyList<TimeTravelTimelineSpan> Calls(TimeTravelTimelineAxis axis)
        => axis == TimeTravelTimelineAxis.Position ? PositionCalls : InstructionCalls;

    public double StartOf(TimeTravelTimelineAxis axis) => Calls(axis)[0].Start;

    public double EndOf(TimeTravelTimelineAxis axis) => Calls(axis)[^1].End;

    public int FirstEndingAfter(TimeTravelTimelineAxis axis, double value, int from = 0)
        => SortedSearch.FirstAfter(axis == TimeTravelTimelineAxis.Position ? PositionEnds : InstructionEnds, value, from);

    public double PositionAt(TimeTravelTimelineAxis axis, double value)
    {
        if (axis == TimeTravelTimelineAxis.Position)
        {
            return value;
        }

        var index = SortedSearch.FirstAfter(InstructionStarts, value) - 1;

        if (index < 0)
        {
            return PositionCalls[0].Start;
        }

        var call = InstructionCalls[index];

        var position = PositionCalls[index];

        if (value <= call.End)
        {
            return Interpolate(value, call.Start, call.End, position.Start, position.End);
        }

        if (index + 1 >= InstructionCalls.Count)
        {
            return position.End;
        }

        return Interpolate(value, call.End, InstructionCalls[index + 1].Start, position.End, PositionCalls[index + 1].Start);
    }

    private static double Interpolate(double value, double from, double to, double mappedFrom, double mappedTo)
        => to <= from ? mappedFrom : mappedFrom + (value - from) / (to - from) * (mappedTo - mappedFrom);
}
