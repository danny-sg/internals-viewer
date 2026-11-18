using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed class MemoryListReader(MemoryHistoryStore store, AddressMap map)
{
    private const int PointerSize = sizeof(ulong);

    private const int MinimumSize = 2 * PointerSize;

    private const int MaximumSize = 8 * PointerSize;

    private const int MaximumSampled = 64;

    private Lock Sync { get; } = new();

    private Dictionary<int, (string Site, int Size)>? Identities { get; set; }

    private Dictionary<(string Site, int Size), int?> NextOffsets { get; } = [];

    public MemoryListNode? GetNode(int block)
    {
        int? nextOffset;

        lock (Sync)
        {
            Identities ??= GetIdentities();

            if (!Identities.TryGetValue(block, out var identity))
            {
                return null;
            }

            if (!NextOffsets.TryGetValue(identity, out nextOffset))
            {
                nextOffset = FindNextOffset(identity);

                NextOffsets[identity] = nextOffset;
            }
        }

        if (nextOffset is not { } next)
        {
            return null;
        }

        var pointers = ReadPointers(block);

        var item = pointers.FindIndex(p => p.Offset != next && p.Block >= 0);

        return item < 0
            ? new MemoryListNode(next, null, null)
            : new MemoryListNode(next, pointers[item].Offset, GetClassName(pointers[item]));
    }

    private Dictionary<int, (string Site, int Size)> GetIdentities()
    {
        var identities = new Dictionary<int, (string Site, int Size)>();

        for (var block = 0; block < store.Blocks.Count; block++)
        {
            var item = store.Blocks[block];

            if (item.IsStack || item.Size is < MinimumSize or > MaximumSize || item.Size % PointerSize != 0)
            {
                continue;
            }

            if (map.GetClassName(item.Address, item.FromPosition, item.ToPosition) is null
                && map.GetLabel(item.Address, item.FromPosition, item.ToPosition) is { } site)
            {
                identities[block] = (site, item.Size);
            }
        }

        return identities;
    }

    private int? FindNextOffset((string Site, int Size) identity)
    {
        var votes = new Dictionary<int, int>();

        var sampled = 0;

        foreach (var (block, other) in Identities!)
        {
            if (other != identity)
            {
                continue;
            }

            if (sampled++ >= MaximumSampled)
            {
                break;
            }

            foreach (var pointer in ReadPointers(block))
            {
                if (pointer.Block >= 0 && Identities.TryGetValue(pointer.Block, out var target) && target == identity)
                {
                    votes[pointer.Offset] = votes.GetValueOrDefault(pointer.Offset) + 1;
                }
            }
        }

        return votes.Count == 0 ? null : votes.MaxBy(v => v.Value).Key;
    }

    private List<MemoryHistoryPointer> ReadPointers(int block)
    {
        var item = store.Blocks[block];

        if (item.WriteCount == 0)
        {
            return [];
        }

        var position = store.GetPoint(item.FirstWrite + item.WriteCount - 1).Position;

        var image = store.GetImage(block, item.WriteCount - 1);

        return MemoryHistoryPointerReader.Find(store, TraceBufferPool.Empty, block, image, item.WriteCount, position);
    }

    private string? GetClassName(MemoryHistoryPointer pointer)
    {
        var target = store.Blocks[pointer.Block];

        return target.Address == pointer.Value
            ? map.GetVtableClassName(target.Address, target.FromPosition, target.ToPosition)
              ?? map.GetClassName(target.Address, target.FromPosition, target.ToPosition)
            : null;
    }
}
