using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Helpers;

internal static class EnclosingSweep
{
    public static ulong[] GetEnclosing<TPosition>(IEnumerable<SweepSpan<TPosition>> spans, IReadOnlyList<SweepSpan<TPosition>> points)
        where TPosition : IComparable<TPosition>
    {
        var enclosing = new ulong[points.Count];

        var threads = new Dictionary<uint, List<Entry<TPosition>>>();

        foreach (var span in spans)
        {
            Add(span, -1);
        }

        for (var index = 0; index < points.Count; index++)
        {
            Add(points[index], index);
        }

        var open = new Stack<Entry<TPosition>>();

        foreach (var thread in threads.Values)
        {
            thread.Sort(Compare);

            open.Clear();

            foreach (var entry in thread)
            {
                while (open.TryPeek(out var outer) && outer.Span.To.CompareTo(entry.Span.From) <= 0)
                {
                    open.Pop();
                }

                if (entry.Point >= 0)
                {
                    enclosing[entry.Point] = open.TryPeek(out var inner) ? inner.Span.InstanceAddress : 0;
                }

                open.Push(entry);
            }
        }

        return enclosing;

        void Add(SweepSpan<TPosition> span, int point)
        {
            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(threads, span.Thread, out _);

            (list ??= []).Add(new Entry<TPosition>(span, point));
        }
    }

    private static int Compare<TPosition>(Entry<TPosition> left, Entry<TPosition> right) where TPosition : IComparable<TPosition>
        => left.Span.From.CompareTo(right.Span.From) is var order and not 0 ? order : right.Span.To.CompareTo(left.Span.To);

    private readonly record struct Entry<TPosition>(SweepSpan<TPosition> Span, int Point);
}
