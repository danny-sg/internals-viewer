using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.MemoryObject;
using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Memory.History.Capture;

internal sealed class WatchedBlockIndex
{
    public const int MaximumIteratorSize = 0x2000;

    internal const int PageShift = 12;

    public WatchedBlockIndex(List<WatchedBlock> blocks)
    {
        Blocks = blocks;

        for (var index = 0; index < blocks.Count; index++)
        {
            if (!blocks[index].IsStack)
            {
                AddToPages(index);
            }
        }
    }

    public List<WatchedBlock> Blocks { get; }

    private Dictionary<ulong, List<int>> Pages { get; } = [];

    public bool HasPage(ulong address) => Pages.ContainsKey(address >> PageShift);

    public bool IsLive(ulong address, ulong size, TracePosition position)
    {
        if (size == 0)
        {
            return false;
        }

        var end = address + size;

        for (var page = address >> PageShift; page <= (end - 1) >> PageShift; page++)
        {
            if (!Pages.TryGetValue(page, out var list))
            {
                continue;
            }

            foreach (var index in list)
            {
                var block = Blocks[index];

                if (block.Address < end
                    && address < block.Address + block.Size
                    && block.FromPosition <= position
                    && position < block.ToPosition)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public int? GetInnermost(ulong address, TracePosition position)
    {
        if (!Pages.TryGetValue(address >> PageShift, out var page))
        {
            return null;
        }

        int? found = null;

        foreach (var index in page)
        {
            if (Blocks[index].Contains(address, position) && (found is not { } current || Blocks[index].Size < Blocks[current].Size))
            {
                found = index;
            }
        }

        return found;
    }

    public ulong GetNextInner(int container, ulong address, TracePosition position)
    {
        var next = ulong.MaxValue;

        if (!Pages.TryGetValue(address >> PageShift, out var page))
        {
            return next;
        }

        var size = Blocks[container].Size;

        foreach (var index in page)
        {
            var block = Blocks[index];

            if (block.Address > address
                && block.Address < next
                && block.Size < size
                && block.FromPosition <= position
                && position < block.ToPosition)
            {
                next = block.Address;
            }
        }

        return next;
    }

    public void EndReusedBlocks(Func<ulong, TracePosition, MemoryObjectReference?>? getPageOwner = null)
    {
        foreach (var index in Enumerable.Range(0, Blocks.Count).OrderBy(i => Blocks[i].FromPosition))
        {
            var block = Blocks[index];

            if (block.IsStack || block.IsInline)
            {
                continue;
            }

            var end = block.Address + block.Size;

            for (var page = block.Address >> PageShift; page <= (end - 1) >> PageShift; page++)
            {
                foreach (var older in Pages.GetValueOrDefault(page) ?? [])
                {
                    var previous = Blocks[older];

                    if (older != index
                        && !previous.IsInline
                        && previous.FromPosition < block.FromPosition
                        && block.FromPosition < previous.ToPosition
                        && previous.Address < end
                        && block.Address < previous.Address + previous.Size
                        && (!IsContainer(previous, block) || IsReclaimed(previous, block, getPageOwner)))
                    {
                        previous.ToPosition = block.FromPosition;
                    }
                }
            }
        }
    }

    public void ClipToContainers()
    {
        foreach (var index in Enumerable.Range(0, Blocks.Count).OrderByDescending(i => Blocks[i].Size))
        {
            var block = Blocks[index];

            if (GetContainer(index) is { } container && Blocks[container].ToPosition < block.ToPosition)
            {
                block.ToPosition = Blocks[container].ToPosition;
            }
        }
    }

    public void AddIterators(IReadOnlyDictionary<ulong, TracePosition> iterators)
    {
        var starts = Blocks.Select(b => b.Address).ToHashSet();

        ulong[] addresses = [.. iterators.Keys.Order()];

        foreach (var (iterator, position) in iterators.OrderBy(i => i.Key))
        {
            if (starts.Contains(iterator) || GetInnermost(iterator, position) is not { } found)
            {
                continue;
            }

            var container = Blocks[found];

            var end = GetNextStart(addresses,
                                   iterator,
                                   Math.Min(container.Address + container.Size, iterator + MaximumIteratorSize),
                                   position);

            Blocks.Add(new WatchedBlock(iterator,
                                        iterator,
                                        end - iterator,
                                        isIterator: true,
                                        isInline: true,
                                        isClerkPage: false,
                                        container.Thread,
                                        container.FromPosition,
                                        container.ToPosition));

            AddToPages(Blocks.Count - 1);
        }
    }

    private ulong GetNextStart(ulong[] iterators, ulong iterator, ulong end, TracePosition position)
    {
        var index = Array.BinarySearch(iterators, iterator);

        var next = index >= 0 && index + 1 < iterators.Length ? Math.Min(end, iterators[index + 1]) : end;

        for (var page = iterator >> PageShift; page <= (next - 1) >> PageShift; page++)
        {
            if (!Pages.TryGetValue(page, out var list))
            {
                continue;
            }

            foreach (var candidate in list)
            {
                var block = Blocks[candidate];

                if (block.Address > iterator && block.Address < next && block.FromPosition <= position && position < block.ToPosition)
                {
                    next = block.Address;
                }
            }
        }

        return next;
    }

    private static bool IsContainer(WatchedBlock container, WatchedBlock block)
        => container.Size > block.Size
           && container.Address <= block.Address
           && block.Address + block.Size <= container.Address + container.Size;

    private static bool IsReclaimed(WatchedBlock container,
                                    WatchedBlock block,
                                    Func<ulong, TracePosition, MemoryObjectReference?>? getPageOwner)
        => getPageOwner?.Invoke(block.Address, container.FromPosition) is { } before
           && container.Size < (ulong)before.PageSize
           && getPageOwner(block.Address, block.FromPosition) is { } after
           && before.Address != after.Address;

    private int? GetContainer(int index)
    {
        var block = Blocks[index];

        if (!Pages.TryGetValue(block.Address >> PageShift, out var page))
        {
            return null;
        }

        int? found = null;

        foreach (var candidate in page)
        {
            var container = Blocks[candidate];

            if (candidate != index
                && container.Size > block.Size
                && container.Contains(block.Address, block.FromPosition)
                && block.Address + block.Size <= container.Address + container.Size
                && (found is not { } current || container.Size < Blocks[current].Size))
            {
                found = candidate;
            }
        }

        return found;
    }

    private void AddToPages(int index)
    {
        var block = Blocks[index];

        for (var page = block.Address >> PageShift; page <= (block.Address + block.Size - 1) >> PageShift; page++)
        {
            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(Pages, page, out _);

            list ??= [];

            list.Add(index);
        }
    }
}
