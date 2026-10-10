namespace InternalsViewer.Query.CallStack.TimeTravel;

public static class Outermost
{
    public static List<T> Of<T>(IEnumerable<T> items, Func<T, double> startOf, Func<T, double> endOf)
    {
        var kept = new List<T>();

        var end = double.MinValue;

        foreach (var item in items.OrderBy(startOf))
        {
            if (startOf(item) < end)
            {
                continue;
            }

            kept.Add(item);

            end = endOf(item);
        }

        return kept;
    }
}
