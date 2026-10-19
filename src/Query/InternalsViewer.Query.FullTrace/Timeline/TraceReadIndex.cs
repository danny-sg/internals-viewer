using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceReadIndex
{
    internal TraceReadIndex(TraceRead[] reads)
    {
        Reads = reads;

        Positions = AxisReads.Build(reads, TraceTimelineAxis.Position);

        Instructions = AxisReads.Build(reads, TraceTimelineAxis.Instructions);
    }

    public int Count => Reads.Length;

    private TraceRead[] Reads { get; }

    private AxisReads Positions { get; }

    private AxisReads Instructions { get; }

    public ReadOnlySpan<double> GetStarts(TraceTimelineAxis axis) => Of(axis).Starts;

    public int GetFirstEndingFrom(TraceTimelineAxis axis, double value) => SortedSearch.FirstAtOrAfter(Of(axis).RunningEnds, value);

    public TraceRead GetRead(TraceTimelineAxis axis, int index) => Reads[Of(axis).Order[index]];

    public TraceRead? GetNearest(TraceTimelineAxis axis, double value, double tolerance)
    {
        var reads = Of(axis);

        TraceRead? nearest = null;

        var best = double.MaxValue;

        for (var index = SortedSearch.FirstAfter(reads.Starts, value + tolerance) - 1;
             index >= 0 && reads.RunningEnds[index] >= value - tolerance;
             index--)
        {
            var read = Reads[reads.Order[index]];

            var span = read.Of(axis);

            var distance = value < span.Start ? span.Start - value : value > span.End ? value - span.End : 0;

            if (distance <= tolerance && distance < best)
            {
                nearest = read;

                best = distance;
            }
        }

        return nearest;
    }

    private AxisReads Of(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? Positions : Instructions;

    private sealed record AxisReads(double[] Starts, double[] RunningEnds, int[] Order)
    {
        public static AxisReads Build(TraceRead[] reads, TraceTimelineAxis axis)
        {
            int[] order = [.. Enumerable.Range(0, reads.Length).OrderBy(i => reads[i].Of(axis).Start)];

            var runningEnds = new double[order.Length];

            var end = double.MinValue;

            for (var index = 0; index < order.Length; index++)
            {
                end = Math.Max(end, reads[order[index]].Of(axis).End);

                runningEnds[index] = end;
            }

            return new AxisReads([.. order.Select(i => reads[i].Of(axis).Start)], runningEnds, order);
        }
    }
}
