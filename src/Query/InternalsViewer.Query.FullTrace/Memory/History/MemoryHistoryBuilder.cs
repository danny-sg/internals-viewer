using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory.Enums;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.History;

internal static class MemoryHistoryBuilder
{
    public const int MaximumIteratorSize = 0x2000;

    public const ulong MaximumBlockSize = 64 << 20;

    private const int PageShift = 12;

    private const ulong PageSize = 1ul << PageShift;

    public static MemoryHistoryAllocation[] GetAllocations(TraceCallStore calls, IEnumerable<MemoryFunction> functions)
    {
        var events = new List<(TracePosition Position, ulong Pointer, ulong Bytes, uint Thread, bool IsClerkPage)>();

        foreach (var function in functions.Where(IsWatched))
        {
            if (calls.GetCallList(function.Address, 0) is not { } list)
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                var call = list[index];

                if (!function.IsCounted(call))
                {
                    continue;
                }

                var thread = list.GetThread(index);

                if (function.Operation != AllocationOperation.Allocate && function.GetPointer(call) is not 0 and var released)
                {
                    events.Add((list.GetFromPosition(index), released, 0, thread, false));
                }

                if (function.IsAllocation
                    && call.ReturnedValue >= TraceAddress.MinAddress
                    && function.GetByteCount(call) is > 0 and <= MaximumBlockSize and var bytes)
                {
                    events.Add((list.GetToPosition(index), call.ReturnedValue, bytes, thread, function.CarriesClerk));
                }
            }
        }

        var live = new Dictionary<ulong, List<(ulong Bytes, uint Thread, TracePosition Start, bool IsClerkPage)>>();

        var allocations = new List<MemoryHistoryAllocation>();

        foreach (var (position, pointer, bytes, thread, isClerkPage) in events.OrderBy(e => e.Position))
        {
            if (bytes == 0)
            {
                if (live.Remove(pointer, out var released))
                {
                    allocations.AddRange(released.Select(r => new MemoryHistoryAllocation(
                                                             new MemoryBlock(pointer, r.Bytes, r.Thread, r.Start, position),
                                                             r.IsClerkPage)));
                }

                continue;
            }

            ref var open = ref CollectionsMarshal.GetValueRefOrAddDefault(live, pointer, out _);

            open ??= [];

            if (!open.Exists(o => o.Bytes == bytes))
            {
                open.Add((bytes, thread, position, isClerkPage));
            }
        }

        foreach (var (pointer, open) in live)
        {
            allocations.AddRange(open.Select(o => new MemoryHistoryAllocation(
                                                     new MemoryBlock(pointer, o.Bytes, o.Thread, o.Start, TracePosition.Unknown),
                                                     o.IsClerkPage)));
        }

        return [.. allocations.OrderBy(a => a.Block.Start).ThenBy(a => a.Block.Pointer)];
    }

    public static WatchRange[] GetRanges(IEnumerable<MemoryBlock> blocks)
    {
        var ranges = new List<WatchRange>();

        foreach (var block in blocks.OrderBy(b => b.Pointer))
        {
            var end = block.Pointer + block.Bytes;

            if (ranges.Count > 0 && ranges[^1].Address + ranges[^1].Size >= block.Pointer)
            {
                var last = ranges[^1];

                ranges[^1] = last with { Size = Math.Max(last.Address + last.Size, end) - last.Address };

                continue;
            }

            ranges.Add(new WatchRange(block.Pointer, block.Bytes));
        }

        return [.. ranges];
    }

    public static Func<WatchHit, bool> GetWatchFilter(IReadOnlyList<MemoryHistoryAllocation> allocations)
    {
        var index = new BlockIndex([.. allocations.Select(a => new WatchedBlock(0,
                                                                              a.Block.Pointer,
                                                                              a.Block.Bytes,
                                                                              isIterator: false,
                                                                              isInline: false,
                                                                              a.IsClerkPage,
                                                                              a.Block.Thread,
                                                                              a.Block.Start,
                                                                              a.Block.FreedAt))]);

        return hit => index.IsLive(hit.Address, hit.Size, hit.Position);
    }

    public static Dictionary<ulong, TracePosition> GetFirstPositions(TraceCallStore calls, IReadOnlySet<ulong> iterators)
    {
        var positions = new Dictionary<ulong, TracePosition>();

        foreach (var (functionAddress, instanceAddress) in calls.FunctionKeys)
        {
            if (!iterators.Contains(instanceAddress) || calls.GetCallList(functionAddress, instanceAddress) is not { Count: > 0 } list)
            {
                continue;
            }

            var position = list.GetFromPosition(0);

            if (!positions.TryGetValue(instanceAddress, out var first) || position < first)
            {
                positions[instanceAddress] = position;
            }
        }

        return positions;
    }

    public static MemoryHistoryStore Build(IReadOnlyList<MemoryHistoryAllocation> allocations,
                                           IReadOnlyList<ulong> owners,
                                           IReadOnlyDictionary<ulong, TracePosition> iterators,
                                           WatchResult watched,
                                           TraceTimeline? timeline)
    {
        var blocks = new List<WatchedBlock>(allocations.Count);

        for (var index = 0; index < allocations.Count; index++)
        {
            var allocation = allocations[index].Block;

            blocks.Add(new WatchedBlock(owners[index],
                                        allocation.Pointer,
                                        allocation.Bytes,
                                        owners[index] == allocation.Pointer,
                                        isInline: false,
                                        allocations[index].IsClerkPage,
                                        allocation.Thread,
                                        allocation.Start,
                                        allocation.FreedAt));
        }

        var lookup = new BlockIndex(blocks);

        lookup.ClipToContainers();

        lookup.AddIterators(iterators);

        var stacks = new StackLookup(timeline);

        foreach (var block in blocks)
        {
            block.IsBufferPool = !block.IsIterator
                                 && (block.IsClerkPage || stacks.IsBufferPool(stacks.GetCallNodeIndex(block.Thread, block.FromPosition)));
        }

        var writes = new ChunkedList<(int Block, MemoryHistoryWrite Write)>();

        var changes = new ChunkedList<MemoryHistoryChange>();

        var data = new List<byte>();

        foreach (var (hit, bytes) in GetOrdered(watched))
        {
            Apply(lookup, stacks, hit, bytes.AsSpan((int)hit.Offset, (int)hit.Size), writes, changes, data);
        }

        return Compact(blocks, stacks, writes, changes, data);
    }

    private static IEnumerable<(WatchHit Hit, byte[] Data)> GetOrdered(WatchResult watched)
        => IsOrdered(watched) ? watched.Batches.SelectMany(b => b.Hits.Select(h => (Hit: h, b.Data))) : GetSorted(watched);

    private static IEnumerable<(WatchHit Hit, byte[] Data)> GetSorted(WatchResult watched)
    {
        var keys = new (TracePosition Position, long Location)[watched.HitCount];

        var at = 0;

        for (var batch = 0; batch < watched.Batches.Count; batch++)
        {
            var hits = watched.Batches[batch].Hits;

            for (var index = 0; index < hits.Length; index++)
            {
                keys[at++] = (hits[index].Position, (long)batch << 32 | (uint)index);
            }
        }

        Array.Sort(keys);

        foreach (var (_, location) in keys)
        {
            var batch = watched.Batches[(int)(location >> 32)];

            yield return (batch.Hits[(int)location], batch.Data);
        }
    }

    private static bool IsOrdered(WatchResult watched)
    {
        var last = default(TracePosition);

        foreach (var batch in watched.Batches)
        {
            foreach (var hit in batch.Hits)
            {
                if (hit.Position < last)
                {
                    return false;
                }

                last = hit.Position;
            }
        }

        return true;
    }

    private static bool IsWatched(MemoryFunction function)
        => !function.IsVirtual
           && (function.IsAllocation || function is { Operation: AllocationOperation.Free, PointerSlot: >= 0 });

    private static void Apply(BlockIndex lookup,
                              StackLookup stacks,
                              WatchHit hit,
                              ReadOnlySpan<byte> bytes,
                              ChunkedList<(int Block, MemoryHistoryWrite Write)> writes,
                              ChunkedList<MemoryHistoryChange> changes,
                              List<byte> data)
    {
        var address = hit.Address;

        (int CallNodeIndex, int CallIndex, ulong Instructions)? writer = null;

        while (bytes.Length > 0)
        {
            var pageEnd = (address & ~(PageSize - 1)) + PageSize;

            if (lookup.GetInnermost(address, hit.Position) is not { } found)
            {
                var skip = lookup.HasPage(address) ? 1 : (int)Math.Min((ulong)bytes.Length, pageEnd - address);

                address += (ulong)skip;

                bytes = bytes[skip..];

                continue;
            }

            var block = lookup.Blocks[found];

            var end = Math.Min(Math.Min(block.Address + block.Size, pageEnd), lookup.GetNextInner(found, address, hit.Position));

            var length = (int)Math.Min((ulong)bytes.Length, end - address);

            var first = changes.Count;

            if (!block.IsBufferPool)
            {
                AddChanges(block, (int)(address - block.Address), bytes[..length], changes, data);
            }

            if (changes.Count > first)
            {
                var (callNodeIndex, callIndex, instructions) = writer ??= stacks.GetWriter(hit.Thread, hit.Position);

                writes.Add((found, new MemoryHistoryWrite(hit.Position, instructions, callNodeIndex, callIndex, first, changes.Count - first)));
            }

            address += (ulong)length;

            bytes = bytes[length..];
        }
    }

    private static void AddChanges(WatchedBlock block,
                                   int offset,
                                   ReadOnlySpan<byte> bytes,
                                   ChunkedList<MemoryHistoryChange> changes,
                                   List<byte> data)
    {
        block.Image ??= new byte[block.Size];

        block.Known ??= new bool[block.Size];

        var position = 0;

        while (position < bytes.Length)
        {
            if (GetChangeType(block, offset + position, bytes[position]) is not { } type)
            {
                position++;

                continue;
            }

            var start = position;

            while (position < bytes.Length && GetChangeType(block, offset + position, bytes[position]) == type)
            {
                position++;
            }

            var run = bytes[start..position];

            run.CopyTo(block.Image.AsSpan(offset + start));

            block.Known.AsSpan(offset + start, run.Length).Fill(true);

            changes.Add(new MemoryHistoryChange(offset + start, run.Length, data.Count, type));

            data.AddRange(run);
        }
    }

    private static MemoryHistoryChangeType? GetChangeType(WatchedBlock block, int offset, byte value)
    {
        if (!block.Known![offset])
        {
            return MemoryHistoryChangeType.Initialised;
        }

        return block.Image![offset] != value ? MemoryHistoryChangeType.Changed : null;
    }

    private static MemoryHistoryStore Compact(List<WatchedBlock> blocks,
                                              StackLookup stacks,
                                              ChunkedList<(int Block, MemoryHistoryWrite Write)> writes,
                                              ChunkedList<MemoryHistoryChange> changes,
                                              List<byte> data)
    {
        var written = new int[blocks.Count];

        for (var index = 0; index < writes.Count; index++)
        {
            written[writes[index].Block]++;
        }

        var next = new int[blocks.Count];

        var kept = new List<MemoryHistoryBlock>();

        var firstWrite = 0;

        for (var index = 0; index < blocks.Count; index++)
        {
            if (written[index] == 0)
            {
                continue;
            }

            next[index] = firstWrite;

            var block = blocks[index];

            var callNodeIndex = stacks.GetCallNodeIndex(block.Thread, block.FromPosition);

            kept.Add(new MemoryHistoryBlock(block.Iterator,
                                            block.Address,
                                            (int)block.Size,
                                            block.IsIterator,
                                            block.IsInline,
                                            block.Thread,
                                            callNodeIndex,
                                            block.FromPosition,
                                            block.ToPosition,
                                            stacks.GetInstructions(block.Thread, block.FromPosition),
                                            block.ToPosition.IsKnown ? stacks.GetInstructions(block.Thread, block.ToPosition) : 0,
                                            firstWrite,
                                            written[index]));

            firstWrite += written[index];
        }

        var grouped = new MemoryHistoryWrite[writes.Count];

        var iterators = new Dictionary<int, ulong>();

        for (var index = 0; index < writes.Count; index++)
        {
            var (block, write) = writes[index];

            grouped[next[block]++] = write;

            if (write.CallNodeIndex >= 0 && stacks.GetIterator(write.CallNodeIndex) is not 0 and var iterator)
            {
                iterators[write.CallNodeIndex] = iterator;
            }
        }

        return new MemoryHistoryStore([.. kept], grouped, changes.ToArray(), [.. data], iterators);
    }

    private sealed class StackLookup(TraceTimeline? timeline)
    {
        private Dictionary<int, ulong> Iterators { get; } = [];

        private Dictionary<int, bool> BufferPool { get; } = [];

        public (int CallNodeIndex, int CallIndex, ulong Instructions) GetWriter(uint thread, TracePosition position)
        {
            if (timeline?.GetInnermostCall(thread, position) is not { } span)
            {
                return (-1, -1, 0);
            }

            return (span.CallNodeIndex, span.CallIndex, GetInstructions(span, position));
        }

        public ulong GetInstructions(uint thread, TracePosition position)
            => timeline?.GetInnermostCall(thread, position) is { } span ? GetInstructions(span, position) : 0;

        private ulong GetInstructions(TraceSpan span, TracePosition position)
        {
            var scale = timeline!.Scale;

            var from = scale.GetValue(span.From.Position);

            var to = scale.GetValue(span.To.Position);

            var fraction = to > from ? Math.Clamp((scale.GetValue(position) - from) / (to - from), 0, 1) : 0;

            return span.From.Instructions + (ulong)((span.To.Instructions - span.From.Instructions) * fraction);
        }

        public int GetCallNodeIndex(uint thread, TracePosition position)
            => timeline?.GetInnermostCall(thread, position) is { } span ? span.CallNodeIndex : -1;

        public bool IsBufferPool(int callNodeIndex)
        {
            if (!BufferPool.TryGetValue(callNodeIndex, out var isBufferPool))
            {
                isBufferPool = timeline?.NodeOf(callNodeIndex)?.Ancestors().Any(n => TraceReads.IsBufferPool(n.Frame)) ?? false;

                BufferPool[callNodeIndex] = isBufferPool;
            }

            return isBufferPool;
        }

        public ulong GetIterator(int callNodeIndex)
        {
            if (!Iterators.TryGetValue(callNodeIndex, out var iterator))
            {
                iterator = timeline?.NodeOf(callNodeIndex)?.Ancestors().FirstOrDefault(IsIteratorCall)?.Frame!.InstanceAddress ?? 0;

                Iterators[callNodeIndex] = iterator;
            }

            return iterator;
        }

        private static bool IsIteratorCall(CallStackNode node)
            => node.Frame is { InstanceAddress: not 0, Resolved.ClassName: { } className } && IteratorSymbols.IsIterator(className);
    }

    private sealed class WatchedBlock(ulong iterator,
                                      ulong address,
                                      ulong size,
                                      bool isIterator,
                                      bool isInline,
                                      bool isClerkPage,
                                      uint thread,
                                      TracePosition fromPosition,
                                      TracePosition toPosition)
    {
        public ulong Iterator { get; } = iterator;

        public ulong Address { get; } = address;

        public ulong Size { get; } = size;

        public bool IsIterator { get; } = isIterator;

        public bool IsInline { get; } = isInline;

        public bool IsClerkPage { get; } = isClerkPage;

        public uint Thread { get; } = thread;

        public TracePosition FromPosition { get; } = fromPosition;

        public TracePosition ToPosition { get; set; } = toPosition;

        public bool IsBufferPool { get; set; }

        public byte[]? Image { get; set; }

        public bool[]? Known { get; set; }

        public bool Contains(ulong address, TracePosition position)
            => address >= Address && address - Address < Size && FromPosition <= position && position < ToPosition;
    }

    private sealed class BlockIndex
    {
        public BlockIndex(List<WatchedBlock> blocks)
        {
            Blocks = blocks;

            for (var index = 0; index < blocks.Count; index++)
            {
                AddToPages(index);
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
}
