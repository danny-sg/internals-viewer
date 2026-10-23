using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline.Ticks;

public sealed class TraceMarkIndex<T> where T : ITraceMark
{
    internal TraceMarkIndex(IEnumerable<T> marks, TraceScale scale)
    {
        Marks = [.. marks.OrderBy(m => m.Span.From.Position)];

        Scale = scale;

        Froms = [.. Marks.Select(m => m.Span.From)];

        RunningEnds = new TracePoint[Marks.Length];

        for (var index = 0; index < Marks.Length; index++)
        {
            var to = Marks[index].Span.To;

            RunningEnds[index] = index > 0 && RunningEnds[index - 1].Position > to.Position ? RunningEnds[index - 1] : to;
        }
    }

    public int Count => Marks.Length;

    public IReadOnlyList<T> All => Marks;

    public TraceScale Scale { get; }

    private T[] Marks { get; }

    private TracePoint[] Froms { get; }

    private TracePoint[] RunningEnds { get; }

    public T Get(int index) => Marks[index];

    public double GetStart(TraceTimelineAxis axis, int index) => Scale.GetValue(Froms[index], axis);

    public int GetFirstEndingFrom(TraceTimelineAxis axis, double value) => Scale.GetFirstAtOrAfter(RunningEnds, axis, value);

    public int GetFirstStartingAfter(TraceTimelineAxis axis, double value) => Scale.GetFirstAfter(Froms, axis, value);

    public T? GetNearest(TraceTimelineAxis axis, double value, double tolerance)
    {
        T? nearest = default;

        var best = double.MaxValue;

        for (var index = Scale.GetFirstAfter(Froms, axis, value + tolerance) - 1;
             index >= 0 && Scale.GetValue(RunningEnds[index], axis) >= value - tolerance;
             index--)
        {
            var span = Marks[index].Span;

            var start = Scale.GetValue(span.From, axis);

            var end = Scale.GetValue(span.To, axis);

            var distance = value < start ? start - value : value > end ? value - end : 0;

            if (distance <= tolerance && distance < best)
            {
                nearest = Marks[index];

                best = distance;
            }
        }

        return nearest;
    }
}
