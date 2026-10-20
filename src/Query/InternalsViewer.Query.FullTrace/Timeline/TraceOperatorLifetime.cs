using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed record TraceOperatorLifetime(uint Thread,
                                           ExecutionOperatorEvent Operator,
                                           int Node,
                                           IReadOnlyList<TraceSpan> PositionCalls,
                                           IReadOnlyList<TraceSpan> InstructionCalls,
                                           MemoryInUseCurve InUse)
{
    public double ConstructedPosition { get; init; } = PositionCalls[0].Start;

    public double ConstructedInstructions { get; init; } = InstructionCalls[0].Start;

    public double DestroyedPosition { get; init; } = PositionCalls[^1].End;

    public double DestroyedInstructions { get; init; } = InstructionCalls[^1].End;

    private double[] PositionEnds { get; } = [.. PositionCalls.Select(c => c.End)];

    private double[] InstructionStarts { get; } = [.. InstructionCalls.Select(c => c.Start)];

    private double[] InstructionEnds { get; } = [.. InstructionCalls.Select(c => c.End)];

    public IReadOnlyList<TraceSpan> Calls(TraceTimelineAxis axis)
        => axis == TraceTimelineAxis.Position ? PositionCalls : InstructionCalls;

    public double StartOf(TraceTimelineAxis axis) => Calls(axis)[0].Start;

    public double EndOf(TraceTimelineAxis axis) => Calls(axis)[^1].End;

    public double GetConstructed(TraceTimelineAxis axis)
        => axis == TraceTimelineAxis.Position ? ConstructedPosition : ConstructedInstructions;

    public double GetDestroyed(TraceTimelineAxis axis)
        => axis == TraceTimelineAxis.Position ? DestroyedPosition : DestroyedInstructions;

    public int FirstEndingAfter(TraceTimelineAxis axis, double value, int from = 0)
        => SortedSearch.FirstAfter(axis == TraceTimelineAxis.Position ? PositionEnds : InstructionEnds, value, from);

    public double PositionAt(TraceTimelineAxis axis, double value)
    {
        if (axis == TraceTimelineAxis.Position)
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
