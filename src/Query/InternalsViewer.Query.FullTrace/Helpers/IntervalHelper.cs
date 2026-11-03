namespace InternalsViewer.Query.FullTrace.Helpers;

public static class IntervalHelper
{
    public static List<(TKey Start, TKey End)> Coalesce<T, TKey>(IEnumerable<T> items, Func<T, TKey> getStart, Func<T, TKey> getEnd)
        where TKey : IComparable<TKey>
    {
        var merged = new List<(TKey Start, TKey End)>();

        foreach (var item in items.OrderBy(getStart))
        {
            var start = getStart(item);

            var end = getEnd(item);

            if (merged.Count > 0 && start.CompareTo(merged[^1].End) <= 0)
            {
                if (end.CompareTo(merged[^1].End) > 0)
                {
                    merged[^1] = (merged[^1].Start, end);
                }

                continue;
            }

            merged.Add((start, end));
        }

        return merged;
    }
}
