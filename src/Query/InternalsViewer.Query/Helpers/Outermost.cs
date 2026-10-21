namespace InternalsViewer.Query.Helpers;

public static class Outermost
{
    public static List<T> Of<T, TKey>(IEnumerable<T> items, Func<T, TKey> getStart, Func<T, TKey> getEnd) where TKey : IComparable<TKey>
    {
        var kept = new List<T>();

        var end = default(TKey)!;

        foreach (var item in items.OrderBy(getStart))
        {
            if (kept.Count > 0 && getStart(item).CompareTo(end) < 0)
            {
                continue;
            }

            kept.Add(item);

            end = getEnd(item);
        }

        return kept;
    }
}
