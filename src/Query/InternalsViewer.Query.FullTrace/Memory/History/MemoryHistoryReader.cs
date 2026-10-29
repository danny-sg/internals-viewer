using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed class MemoryHistoryReader(MemoryHistoryStore store, TracePosition position) : IMemoryReader
{
    public TracePosition Position { get; } = position;

    private Dictionary<int, (MemoryHistoryImage Image, int WriteCount, bool[] IsRead)> Images { get; } = [];

    public int Read(ulong address, Span<byte> destination)
    {
        var block = store.GetBlock(address, Position);

        if (block < 0)
        {
            return 0;
        }

        var (image, _, isRead) = GetImage(block);

        var offset = (int)(address - store.Blocks[block].Address);

        var available = Math.Min(destination.Length, image.Data.Length - offset);

        isRead.AsSpan(offset, available).Fill(true);

        var unknown = image.Known.AsSpan(offset, available).IndexOf(false);

        var count = unknown < 0 ? available : unknown;

        image.Data.AsSpan(offset, count).CopyTo(destination);

        return count;
    }

    public bool HasChanged(TracePosition position)
    {
        foreach (var (block, read) in Images)
        {
            if (!store.IsLive(block, position))
            {
                return true;
            }

            var count = store.GetWriteCount(block, position);

            if (count != read.WriteCount
                && IsAnyWritten(store.GetWrites(block), read.IsRead, Math.Min(count, read.WriteCount), Math.Max(count, read.WriteCount)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAnyWritten(IReadOnlyList<MemoryHistoryWrite> writes, bool[] isRead, int from, int to)
    {
        for (var index = from; index < to; index++)
        {
            if (isRead.AsSpan(writes[index].Offset, writes[index].Length).Contains(true))
            {
                return true;
            }
        }

        return false;
    }

    private (MemoryHistoryImage Image, int WriteCount, bool[] IsRead) GetImage(int block)
    {
        if (Images.TryGetValue(block, out var read))
        {
            return read;
        }

        var count = store.GetWriteCount(block, Position);

        var image = store.GetImage(block, count - 1);

        read = (image, count, new bool[image.Data.Length]);

        Images[block] = read;

        return read;
    }
}
