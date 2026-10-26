using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed class MemoryHistoryReader(MemoryHistoryStore store, TracePosition position) : IMemoryReader
{
    public TracePosition Position { get; } = position;

    private Dictionary<int, (MemoryHistoryImage Image, int WriteCount)> Images { get; } = [];

    public int Read(ulong address, Span<byte> destination)
    {
        var block = store.GetBlock(address, Position);

        if (block < 0)
        {
            return 0;
        }

        var image = GetImage(block);

        var offset = (int)(address - store.Blocks[block].Address);

        var available = Math.Min(destination.Length, image.Data.Length - offset);

        var unknown = image.Known.AsSpan(offset, available).IndexOf(false);

        var count = unknown < 0 ? available : unknown;

        image.Data.AsSpan(offset, count).CopyTo(destination);

        return count;
    }

    public bool HasChanged(TracePosition position)
    {
        foreach (var (block, read) in Images)
        {
            if (!store.IsLive(block, position) || store.GetWriteCount(block, position) != read.WriteCount)
            {
                return true;
            }
        }

        return false;
    }

    private MemoryHistoryImage GetImage(int block)
    {
        if (Images.TryGetValue(block, out var read))
        {
            return read.Image;
        }

        var count = store.GetWriteCount(block, Position);

        var image = store.GetImage(block, count - 1);

        Images[block] = (image, count);

        return image;
    }
}
