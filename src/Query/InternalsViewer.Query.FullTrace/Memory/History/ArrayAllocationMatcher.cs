namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed class ArrayAllocationMatcher(MemoryHistoryStore store, MemoryMap map)
{
    private const string ArrayAllocator = "operator new[]";

    private const int CountSize = sizeof(ulong);

    private Dictionary<int, string?> Blocks { get; } = [];

    public string? GetClassName(int block)
    {
        if (!Blocks.TryGetValue(block, out var className))
        {
            className = FindClassName(block);

            Blocks[block] = className;
        }

        return className;
    }

    private string? FindClassName(int block)
    {
        var allocation = store.Blocks[block];

        if (map.GetAllocator(allocation.Address, allocation.FromPosition, allocation.ToPosition) != ArrayAllocator
            || map.GetClassName(allocation.Address + CountSize, allocation.FromPosition, allocation.ToPosition) is not { } element)
        {
            return null;
        }

        var count = GetCount(block);

        return count > 0 ? $"{element}[{count}]" : null;
    }

    private uint GetCount(int block)
    {
        var elementBytes = store.Blocks[block].Size - CountSize;

        foreach (var write in store.GetWrites(block))
        {
            if (store.TryGetWrittenValue(write, 0, out var count)
                && store.TryGetWrittenValue(write, sizeof(uint), out var high)
                && high == 0
                && count > 0
                && elementBytes % count == 0)
            {
                return count;
            }
        }

        return 0;
    }
}
