using System.Numerics;

namespace InternalsViewer.Execution.BatchMode;

/// <summary>
/// Bitmap marking selected rows packed into 64 bit ulong array
/// </summary>
public sealed class SelectionBitmap
{
    private const int BitsPerWord = 64;

    public SelectionBitmap(int rowCount)
    {
        RowCount = rowCount;

        Words = new ulong[(rowCount + BitsPerWord - 1) / BitsPerWord];

        Array.Fill(Words, ulong.MaxValue);

        var tail = rowCount % BitsPerWord;

        if (tail != 0)
        {
            Words[^1] = (1UL << tail) - 1;
        }
    }

    public int RowCount { get; }

    public ulong[] Words { get; }

    public int Count
    {
        get
        {
            var count = 0;

            foreach (var word in Words)
            {
                count += BitOperations.PopCount(word);
            }

            return count;
        }
    }

    public bool IsSet(int index) 
        => (Words[index / BitsPerWord] & (1UL << (index % BitsPerWord))) != 0;

    public void Clear(int index) 
        => Words[index / BitsPerWord] &= ~(1UL << (index % BitsPerWord));

    public void Set(int index) 
        => Words[index / BitsPerWord] |= 1UL << (index % BitsPerWord);

    public void ClearAll() => Array.Clear(Words);

    public int GetNextSetIndex(int from)
    {
        if (from < 0)
        {
            from = 0;
        }

        var word = from / BitsPerWord;

        if (word >= Words.Length)
        {
            return -1;
        }

        var bits = Words[word] & (ulong.MaxValue << (from % BitsPerWord));

        while (bits == 0)
        {
            word++;

            if (word >= Words.Length)
            {
                return -1;
            }

            bits = Words[word];
        }

        return (word * BitsPerWord) + BitOperations.TrailingZeroCount(bits);
    }
}
