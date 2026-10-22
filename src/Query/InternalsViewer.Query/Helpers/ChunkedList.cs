using System.Collections;
using System.Numerics;

namespace InternalsViewer.Query.Helpers;

public sealed class ChunkedList<T> : IReadOnlyList<T>
{
    private const int FirstShift = 4;

    public int Count { get; private set; }

    private List<T[]> Chunks { get; } = [];

    private int Filled { get; set; }

    public ref T this[int index]
    {
        get
        {
            var chunk = BitOperations.Log2((uint)(index >> FirstShift) + 1);

            return ref Chunks[chunk][index - ((1 << (chunk + FirstShift)) - (1 << FirstShift))];
        }
    }

    T IReadOnlyList<T>.this[int index] => this[index];

    public void Add(T item)
    {
        if (Chunks.Count == 0 || Filled == Chunks[^1].Length)
        {
            Chunks.Add(new T[1 << (Chunks.Count + FirstShift)]);

            Filled = 0;
        }

        Chunks[^1][Filled++] = item;

        Count++;
    }

    public void CopyTo(Span<T> destination)
    {
        var offset = 0;

        for (var chunk = 0; chunk < Chunks.Count; chunk++)
        {
            var length = chunk == Chunks.Count - 1 ? Filled : Chunks[chunk].Length;

            Chunks[chunk].AsSpan(0, length).CopyTo(destination[offset..]);

            offset += length;
        }
    }

    public T[] ToArray()
    {
        var items = new T[Count];

        CopyTo(items);

        return items;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var chunk = 0; chunk < Chunks.Count; chunk++)
        {
            var length = chunk == Chunks.Count - 1 ? Filled : Chunks[chunk].Length;

            for (var index = 0; index < length; index++)
            {
                yield return Chunks[chunk][index];
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
