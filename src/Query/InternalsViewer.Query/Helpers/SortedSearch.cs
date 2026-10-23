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

    public static int FirstAtOrAfter(ReadOnlySpan<ulong> values, ulong value, int from = 0)
        => LowerBound(values, value, from, strict: false);

    public static int FirstAfter(ReadOnlySpan<ulong> values, ulong value, int from = 0)
        => LowerBound(values, value, from, strict: true);

    public static int FirstAtOrAfter<T>(ReadOnlySpan<T> values, T value, int from = 0) where T : IComparable<T>
        => ComparableLowerBound(values, value, from, strict: false);

    public static int FirstAfter<T>(ReadOnlySpan<T> values, T value, int from = 0) where T : IComparable<T>
        => ComparableLowerBound(values, value, from, strict: true);

    public static int FirstAtOrAfter<T, TKey>(ReadOnlySpan<T> values, TKey value, Func<T, TKey> getKey, int from = 0)
        where TKey : IComparable<TKey>
        => KeyLowerBound(values, value, getKey, from, strict: false);

    public static int FirstAfter<T, TKey>(ReadOnlySpan<T> values, TKey value, Func<T, TKey> getKey, int from = 0)
        where TKey : IComparable<TKey>
        => KeyLowerBound(values, value, getKey, from, strict: true);

    private static int KeyLowerBound<T, TKey>(ReadOnlySpan<T> values, TKey value, Func<T, TKey> getKey, int from, bool strict)
        where TKey : IComparable<TKey>
    {
        var low = from;

        var high = values.Length;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            var comparison = getKey(values[middle]).CompareTo(value);

            if (strict ? comparison <= 0 : comparison < 0)
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

    private static int ComparableLowerBound<T>(ReadOnlySpan<T> values, T value, int from, bool strict) where T : IComparable<T>
    {
        var low = from;

        var high = values.Length;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            var comparison = values[middle].CompareTo(value);

            if (strict ? comparison <= 0 : comparison < 0)
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
