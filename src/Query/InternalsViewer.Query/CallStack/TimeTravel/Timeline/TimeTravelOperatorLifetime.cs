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
    public IReadOnlyList<TimeTravelTimelineSpan> Calls(TimeTravelTimelineAxis axis)
        => axis == TimeTravelTimelineAxis.Position ? PositionCalls : InstructionCalls;

    public double StartOf(TimeTravelTimelineAxis axis) => Calls(axis)[0].Start;

    public double EndOf(TimeTravelTimelineAxis axis) => Calls(axis)[Calls(axis).Count - 1].End;

    public double PositionAt(TimeTravelTimelineAxis axis, double value)
    {
        if (axis == TimeTravelTimelineAxis.Position)
        {
            return value;
        }

        var low = 0;

        var high = InstructionCalls.Count;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            if (InstructionCalls[middle].Start <= value)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        var index = low - 1;

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
