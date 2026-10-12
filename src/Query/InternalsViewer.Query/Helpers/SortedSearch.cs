using System.Numerics;

namespace InternalsViewer.Query.Helpers;

public static class SortedSearch
{
    public static int FirstAtOrAfter(ReadOnlySpan<double> values, double value, int from = 0)
        => LowerBound(values, value, from, strict: false);

    public static int FirstAfter(ReadOnlySpan<double> values, double value, int from = 0)
        => LowerBound(values, value, from, strict: true);

    public static int FirstAtOrAfter(ReadOnlySpan<int> values, int value, int from = 0)
        => LowerBound(values, value, from, strict: false);

    public static int FirstAfter(ReadOnlySpan<int> values, int value, int from = 0)
        => LowerBound(values, value, from, strict: true);

    private static int LowerBound<T>(ReadOnlySpan<T> values, T value, int from, bool strict) where T : INumber<T>
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
