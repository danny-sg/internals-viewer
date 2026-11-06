using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceScale(double stepsPerSequence)
{
    public static TraceScale Default { get; } = new(1);

    public double StepsPerSequence { get; } = stepsPerSequence;

    public IReadOnlyList<double> Cuts => CutAt;

    private double[] CutStarts { get; set; } = [];

    private double[] CutEnds { get; set; } = [];

    private double[] CutAt { get; set; } = [];

    private double[] CutBefore { get; set; } = [];

    public double GetStep(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? 1 / StepsPerSequence : 1;

    public double GetValue(TracePosition position) => Cut(GetRecordedValue(position));

    public double GetValue(TracePoint point, TraceTimelineAxis axis)
        => axis == TraceTimelineAxis.Position ? GetValue(point.Position) : point.Instructions;

    public double GetValue(TracePosition position, TraceThreadClock clock, TraceTimelineAxis axis)
        => axis == TraceTimelineAxis.Position ? GetValue(position) : clock.GetInstructions(position);

    public TracePosition GetPosition(double value)
    {
        var recorded = Uncut(value);

        var sequence = Math.Floor(Math.Clamp(recorded, 0, TracePosition.MaximumSequence));

        var steps = Math.Clamp(Math.Round((recorded - sequence) * StepsPerSequence), 0, Math.Max(StepsPerSequence - 1, 0));

        return new TracePosition((ulong)sequence, (ulong)steps);
    }

    public int GetFirstAtOrAfter(ReadOnlySpan<TracePoint> points, TraceTimelineAxis axis, double value, int from = 0)
        => GetLowerBound(points, axis, value, from, strict: false);

    public int GetFirstAfter(ReadOnlySpan<TracePoint> points, TraceTimelineAxis axis, double value, int from = 0)
        => GetLowerBound(points, axis, value, from, strict: true);

    internal double GetRecordedValue(TracePosition position)
        => position.Sequence + Math.Min(position.Steps, (ulong)Math.Max(StepsPerSequence - 1, 0)) / StepsPerSequence;

    internal void SetCuts(IEnumerable<(TracePosition From, TracePosition To)> cuts)
    {
        var merged = new List<(double Start, double End)>();

        foreach (var (from, to) in cuts.Select(c => (GetRecordedValue(c.From), GetRecordedValue(c.To))).OrderBy(c => c.Item1))
        {
            if (merged.Count > 0 && from <= merged[^1].End)
            {
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, to));
            }
            else if (to > from)
            {
                merged.Add((from, to));
            }
        }

        CutStarts = [.. merged.Select(c => c.Start)];

        CutEnds = [.. merged.Select(c => c.End)];

        CutBefore = new double[merged.Count];

        CutAt = new double[merged.Count];

        var removed = 0d;

        for (var index = 0; index < merged.Count; index++)
        {
            CutBefore[index] = removed;

            CutAt[index] = CutStarts[index] - removed;

            removed += CutEnds[index] - CutStarts[index];
        }
    }

    private double Cut(double value)
    {
        var index = GetLast(CutStarts, value);

        if (index < 0)
        {
            return value;
        }

        return value < CutEnds[index] ? CutAt[index] : value - CutBefore[index] - (CutEnds[index] - CutStarts[index]);
    }

    private double Uncut(double value)
    {
        var index = GetLast(CutAt, value);

        return index < 0 ? value : value + CutBefore[index] + (CutEnds[index] - CutStarts[index]);
    }

    private static int GetLast(double[] values, double value)
    {
        if (values.Length == 0)
        {
            return -1;
        }

        var index = Array.BinarySearch(values, value);

        return index >= 0 ? index : ~index - 1;
    }

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
}
