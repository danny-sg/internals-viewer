using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceScale(double stepsPerSequence)
{
    public static TraceScale Default { get; } = new(1);

    public double StepsPerSequence { get; } = stepsPerSequence;

    public double GetStep(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? 1 / StepsPerSequence : 1;

    public double GetValue(TracePosition position)
        => position.Sequence + Math.Min(position.Steps, (ulong)Math.Max(StepsPerSequence - 1, 0)) / StepsPerSequence;

    public double GetValue(TracePoint point, TraceTimelineAxis axis)
        => axis == TraceTimelineAxis.Position ? GetValue(point.Position) : point.Instructions;

    public double GetValue(TracePosition position, TraceThreadClock clock, TraceTimelineAxis axis)
        => axis == TraceTimelineAxis.Position ? GetValue(position) : clock.GetInstructions(position);

    public TracePosition GetPosition(double value)
    {
        var sequence = Math.Floor(Math.Clamp(value, 0, TracePosition.MaximumSequence));

        var steps = Math.Clamp(Math.Round((value - sequence) * StepsPerSequence), 0, Math.Max(StepsPerSequence - 1, 0));

        return new TracePosition((ulong)sequence, (ulong)steps);
    }

    public int GetFirstAtOrAfter(ReadOnlySpan<TracePoint> points, TraceTimelineAxis axis, double value, int from = 0)
        => GetLowerBound(points, axis, value, from, strict: false);

    public int GetFirstAfter(ReadOnlySpan<TracePoint> points, TraceTimelineAxis axis, double value, int from = 0)
        => GetLowerBound(points, axis, value, from, strict: true);

    public int GetFirstAtOrAfter(ReadOnlySpan<TracePosition> positions,
                                 TraceThreadClock clock,
                                 TraceTimelineAxis axis,
                                 double value,
                                 int from = 0)
        => GetLowerBound(positions, clock, axis, value, from, strict: false);

    public int GetFirstAfter(ReadOnlySpan<TracePosition> positions,
                             TraceThreadClock clock,
                             TraceTimelineAxis axis,
                             double value,
                             int from = 0)
        => GetLowerBound(positions, clock, axis, value, from, strict: true);

    private int GetLowerBound(ReadOnlySpan<TracePoint> points, TraceTimelineAxis axis, double value, int from, bool strict)
    {
        var low = from;

        var high = points.Length;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            var probe = GetValue(points[middle], axis);

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

    private int GetLowerBound(ReadOnlySpan<TracePosition> positions,
                              TraceThreadClock clock,
                              TraceTimelineAxis axis,
                              double value,
                              int from,
                              bool strict)
    {
        var low = from;

        var high = positions.Length;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            var probe = GetValue(positions[middle], clock, axis);

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
}
