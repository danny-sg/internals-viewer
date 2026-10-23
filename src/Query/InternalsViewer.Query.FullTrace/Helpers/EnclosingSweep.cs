namespace InternalsViewer.Query.FullTrace.Helpers;

internal static class EnclosingSweep
{
    public static ulong[] GetEnclosing<TPosition>(List<SweepSpan<TPosition>> spans, SweepSpan<TPosition>[] points)
        where TPosition : IComparable<TPosition>
    {
        var enclosing = new ulong[points.Length];

        spans.Sort(static (left, right) => Compare(left, right));

        var order = new int[points.Length];

        for (var index = 0; index < order.Length; index++)
        {
            order[index] = index;
        }

        Array.Sort(order, (left, right) => Compare(points[left], points[right]));

        var open = new Stack<SweepSpan<TPosition>>();

        var next = 0;

        uint? thread = null;

        foreach (var pointIndex in order)
        {
            var point = points[pointIndex];

            if (thread != point.Thread)
            {
                thread = point.Thread;

                open.Clear();
            }

            for (; next < spans.Count && Compare(spans[next], point) < 0; next++)
            {
                if (spans[next].Thread == point.Thread)
                {
                    Close(open, spans[next].From);

                    open.Push(spans[next]);
                }
            }

            Close(open, point.From);

            enclosing[pointIndex] = open.TryPeek(out var inner) ? inner.InstanceAddress : 0;

            open.Push(point);
        }

        return enclosing;
    }

    private static int Compare<TPosition>(SweepSpan<TPosition> left, SweepSpan<TPosition> right) where TPosition : IComparable<TPosition>
    {
        if (left.Thread != right.Thread)
        {
            return left.Thread.CompareTo(right.Thread);
        }

        return left.From.CompareTo(right.From) is var order and not 0 ? order : right.To.CompareTo(left.To);
    }

    private static void Close<TPosition>(Stack<SweepSpan<TPosition>> open, TPosition position) where TPosition : IComparable<TPosition>
    {
        while (open.TryPeek(out var outer) && outer.To.CompareTo(position) <= 0)
        {
            open.Pop();
        }
    }
}
