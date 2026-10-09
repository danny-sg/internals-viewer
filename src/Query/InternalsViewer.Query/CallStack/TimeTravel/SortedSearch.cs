namespace InternalsViewer.Query.CallStack.TimeTravel;

public static class SortedSearch
{
    public static int FirstAtOrAfter(ReadOnlySpan<double> values, double value, int from = 0)
        => LowerBound(values, value, from, strict: false);

    public static int FirstAfter(ReadOnlySpan<double> values, double value, int from = 0)
        => LowerBound(values, value, from, strict: true);

    private static int LowerBound(ReadOnlySpan<double> values, double value, int from, bool strict)
    {
        var low = from;

        var high = values.Length;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            if (strict ? values[middle] <= value : values[middle] < value)
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
