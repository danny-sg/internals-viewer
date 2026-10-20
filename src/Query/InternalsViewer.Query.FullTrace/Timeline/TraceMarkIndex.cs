using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceMarkIndex<T> where T : ITraceMark
{
    internal TraceMarkIndex(T[] marks)
    {
        Marks = marks;

        Positions = AxisMarks.Build(marks, TraceTimelineAxis.Position);

        Instructions = AxisMarks.Build(marks, TraceTimelineAxis.Instructions);
    }

    public int Count => Marks.Length;

    public IReadOnlyList<T> All => Marks;

    private T[] Marks { get; }

    private AxisMarks Positions { get; }

    private AxisMarks Instructions { get; }

    public ReadOnlySpan<double> GetStarts(TraceTimelineAxis axis) => Of(axis).Starts;

    public int GetFirstEndingFrom(TraceTimelineAxis axis, double value) => SortedSearch.FirstAtOrAfter(Of(axis).RunningEnds, value);

    public T Get(TraceTimelineAxis axis, int index) => Marks[Of(axis).Order[index]];

    public T? GetNearest(TraceTimelineAxis axis, double value, double tolerance)
    {
        var marks = Of(axis);

        T? nearest = default;

        var best = double.MaxValue;

        for (var index = SortedSearch.FirstAfter(marks.Starts, value + tolerance) - 1;
             index >= 0 && marks.RunningEnds[index] >= value - tolerance;
             index--)
        {
            var mark = Marks[marks.Order[index]];

            var span = mark.Of(axis);

            var distance = value < span.Start ? span.Start - value : value > span.End ? value - span.End : 0;

            if (distance <= tolerance && distance < best)
            {
                nearest = mark;

                best = distance;
            }
        }

        return nearest;
    }

    private AxisMarks Of(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? Positions : Instructions;

    private sealed record AxisMarks(double[] Starts, double[] RunningEnds, int[] Order)
    {
        public static AxisMarks Build(T[] marks, TraceTimelineAxis axis)
        {
            int[] order = [.. Enumerable.Range(0, marks.Length).OrderBy(i => marks[i].Of(axis).Start)];

            var runningEnds = new double[order.Length];

            var end = double.MinValue;

            for (var index = 0; index < order.Length; index++)
            {
                end = Math.Max(end, marks[order[index]].Of(axis).End);

                runningEnds[index] = end;
            }

            return new AxisMarks([.. order.Select(i => marks[i].Of(axis).Start)], runningEnds, order);
        }
    }
}
