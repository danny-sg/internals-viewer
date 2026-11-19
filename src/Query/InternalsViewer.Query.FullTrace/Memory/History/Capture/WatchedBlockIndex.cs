using InternalsViewer.Query.FullTrace.Memory.MemoryObject;
using System.Numerics;
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

    private Dictionary<ulong, WatchedPage> Pages { get; } = [];

    public bool IsLive(ulong address, ulong size, TracePosition position)
    {
        if (size == 0)
        {
            return false;
        }

        var end = address + size;

        for (var pageNumber = address >> PageShift; pageNumber <= (end - 1) >> PageShift; pageNumber++)
        {
            if (!Pages.TryGetValue(pageNumber, out var page))
            {
                continue;
            }

            for (var at = page.GetLast(position); at >= 0; at = page.GetPrevious(at, position))
            {
                var block = Blocks[page.GetBlock(at)];

                if (block.Address < end && address < block.Address + block.Size)
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

        for (var at = page.GetLast(position); at >= 0; at = page.GetPrevious(at, position))
        {
            var index = page.GetBlock(at);

            var block = Blocks[index];

            if (address >= block.Address && address - block.Address < block.Size && (found is not { } current || IsInside(index, current)))
            {
                found = index;
            }
        }

        return found;
    }

    public ulong GetNextInner(int container, ulong address, TracePosition position)
        => GetNextLiveStart(address, position, Blocks[container].Size);

    public ulong GetNextLiveStart(ulong address, TracePosition position)
        => GetNextLiveStart(address, position, ulong.MaxValue);

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
                foreach (var older in Pages.GetValueOrDefault(page)?.BlockIndexes ?? [])
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

        ResetPages();
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

        ResetPages();
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
            if (!Pages.TryGetValue(page, out var watched))
            {
                continue;
            }

            foreach (var candidate in watched.BlockIndexes)
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

        foreach (var candidate in page.BlockIndexes)
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

    private ulong GetNextLiveStart(ulong address, TracePosition position, ulong size)
    {
        var next = ulong.MaxValue;

        if (!Pages.TryGetValue(address >> PageShift, out var page))
        {
            return next;
        }

        for (var at = page.GetLast(position); at >= 0; at = page.GetPrevious(at, position))
        {
            var block = Blocks[page.GetBlock(at)];

            if (block.Address > address && block.Address < next && block.Size < size)
            {
                next = block.Address;
            }
        }

        return next;
    }

    private bool IsInside(int index, int current)
        => Blocks[index].Size < Blocks[current].Size || (Blocks[index].Size == Blocks[current].Size && index < current);

    private void AddToPages(int index)
    {
        var block = Blocks[index];

        for (var page = block.Address >> PageShift; page <= (block.Address + block.Size - 1) >> PageShift; page++)
        {
            ref var watched = ref CollectionsMarshal.GetValueRefOrAddDefault(Pages, page, out _);

            watched ??= new WatchedPage(Blocks);

            watched.Add(index);
        }
    }

    private void ResetPages()
    {
        foreach (var page in Pages.Values)
        {
            page.Reset();
        }
    }

    private sealed class WatchedPage(List<WatchedBlock> blocks)
    {
        public List<int> BlockIndexes { get; } = [];

        private int[] Order { get; set; } = [];

        private TracePosition[] Starts { get; set; } = [];

        private TracePosition[] Ends { get; set; } = [];

        private int LeafCount { get; set; }

        private bool IsBuilt { get; set; }

        public void Add(int index)
        {
            BlockIndexes.Add(index);

            IsBuilt = false;
        }

        public void Reset() => IsBuilt = false;

        public int GetBlock(int at) => Order[at];

        public int GetLast(TracePosition position)
        {
            if (!IsBuilt)
            {
                Build();
            }

            return GetPrevious(GetStartedCount(position), position);
        }

        public int GetPrevious(int at, TracePosition position)
        {
            if (at <= 0)
            {
                return -1;
            }

            var node = LeafCount + at - 1;

            if (Ends[node] > position)
            {
                return at - 1;
            }

            while (node > 1)
            {
                if ((node & 1) == 1 && Ends[node - 1] > position)
                {
                    node--;

                    while (node < LeafCount)
                    {
                        node = Ends[2 * node + 1] > position ? 2 * node + 1 : 2 * node;
                    }

                    return node - LeafCount;
                }

                node >>= 1;
            }

            return -1;
        }

        private int GetStartedCount(TracePosition position)
        {
            var low = 0;

            var high = Starts.Length;

            while (low < high)
            {
                var middle = low + (high - low) / 2;

                if (Starts[middle] <= position)
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

        private void Build()
        {
            var order = BlockIndexes.ToArray();

            var starts = new TracePosition[order.Length];

            for (var index = 0; index < order.Length; index++)
            {
                starts[index] = blocks[order[index]].FromPosition;
            }

            Array.Sort(starts, order);

            var leafCount = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(order.Length, 1));

            var ends = new TracePosition[2 * leafCount];

            for (var index = 0; index < order.Length; index++)
            {
                ends[leafCount + index] = blocks[order[index]].ToPosition;
            }

            for (var node = leafCount - 1; node > 0; node--)
            {
                ends[node] = ends[2 * node] > ends[2 * node + 1] ? ends[2 * node] : ends[2 * node + 1];
            }

            Order = order;
            Starts = starts;
            Ends = ends;
            LeafCount = leafCount;
            IsBuilt = true;
        }
    }
}
