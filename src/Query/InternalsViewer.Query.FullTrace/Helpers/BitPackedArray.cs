using System.Numerics;

namespace InternalsViewer.Query.FullTrace.Helpers;

internal sealed partial class BitPackedArray
{
    private const int WordBits = 64;

    private BitPackedArray(ulong[] words, int count, int width)
    {
        Words = words;
        Count = count;
        Width = width;
    }

    public int Count { get; }

    public int Width { get; }

    public long Bytes => Words.Length * sizeof(ulong);

    private ulong[] Words { get; }

    public int this[int index]
    {
        get
        {
            if (Width == 0)
            {
                return 0;
            }

            var bit = (long)index * Width;

            var word = (int)(bit / WordBits);

            var shift = (int)(bit % WordBits);

            var value = Words[word] >> shift;

            if (shift + Width > WordBits)
            {
                value |= Words[word + 1] << (WordBits - shift);
            }

            return (int)(value & ((1ul << Width) - 1));
        }
    }

    public static BitPackedArray Pack(ReadOnlySpan<int> values)
    {
        var maximum = 0;

        foreach (var value in values)
        {
            maximum = Math.Max(maximum, value);
        }

        var width = 32 - BitOperations.LeadingZeroCount((uint)maximum);

        var words = new ulong[(int)(((long)values.Length * width + WordBits - 1) / WordBits)];

        for (var index = 0; index < values.Length && width > 0; index++)
        {
            var bit = (long)index * width;

            var word = (int)(bit / WordBits);

            var shift = (int)(bit % WordBits);

            var value = (ulong)(uint)values[index];

            words[word] |= value << shift;

            if (shift + width > WordBits)
            {
                words[word + 1] |= value >> (WordBits - shift);
            }
        }

        return new BitPackedArray(words, values.Length, width);
    }

    public int GetFirstAtOrAfter(int value, int from = 0) => GetLowerBound(value, from, strict: false);

    public int GetFirstAfter(int value, int from = 0) => GetLowerBound(value, from, strict: true);

    private int GetLowerBound(int value, int from, bool strict)
    {
        var low = from;

        var high = Count;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            var probe = this[middle];

            if (strict ? probe <= value : probe < value)
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
