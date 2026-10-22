namespace InternalsViewer.Query.Helpers;

public sealed class ChunkedList<T>
{
    private const int ChunkShift = 16;

    private const int ChunkSize = 1 << ChunkShift;

    private const int ChunkMask = ChunkSize - 1;

    public int Count { get; private set; }

    private List<T[]> Chunks { get; } = [];

    public T this[int index] => Chunks[index >> ChunkShift][index & ChunkMask];

    public void Add(T item)
    {
        if ((Count & ChunkMask) == 0)
        {
            Chunks.Add(new T[ChunkSize]);
        }

        Chunks[^1][Count & ChunkMask] = item;

        Count++;
    }

    public T[] ToArray()
    {
        var items = new T[Count];

        for (var chunk = 0; chunk < Chunks.Count; chunk++)
        {
            var first = chunk << ChunkShift;

            Chunks[chunk].AsSpan(0, Math.Min(ChunkSize, Count - first)).CopyTo(items.AsSpan(first));
        }

        return items;
    }
}
