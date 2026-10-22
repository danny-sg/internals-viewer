using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory.Enums;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.FullTrace.Memory.History.Watches;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;

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

    public static (MemoryHistoryStore Store, MemoryHistoryHitCounts Counts) Build(IReadOnlyList<MemoryHistoryAllocation> allocations,
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

        var recorder = new WriteRecorder(lookup, stacks);

        var order = HitOrder.Get(watched);

        foreach (var (hit, bytes) in order.GetHits())
        {
            recorder.Count(hit, bytes.AsSpan(hit.DataOffset, hit.Size));
        }

        var (kept, firstWrites) = GetKeptBlocks(blocks, stacks, recorder.Written);

        recorder.BeginWriting(firstWrites);

        foreach (var (hit, bytes) in order.GetHits())
        {
            recorder.Write(hit, bytes.AsSpan(hit.DataOffset, hit.Size));
        }

        var store = new MemoryHistoryStore(kept, recorder.Writes, recorder.GetInstructionHighs(), recorder.Data, recorder.Iterators);

        return (store, recorder.Counts);
    }

    private static bool IsWatched(MemoryFunction function)
        => !function.IsVirtual
           && (function.IsAllocation || function is { Operation: AllocationOperation.Free, PointerSlot: >= 0 });

    private static MemoryHistoryChangeType? GetChangeType(WatchedBlock block, int offset, byte value)
    {
        if (!block.Known![offset])
        {
            return MemoryHistoryChangeType.Initialised;
        }

        return block.Image![offset] != value ? MemoryHistoryChangeType.Changed : null;
    }

    private static (MemoryHistoryBlock[] Kept, int[] FirstWrites) GetKeptBlocks(List<WatchedBlock> blocks,
                                                                                StackLookup stacks,
                                                                                int[] written)
    {
        var firstWrites = new int[blocks.Count];

        var kept = new List<MemoryHistoryBlock>();

        var firstWrite = 0;

        for (var index = 0; index < blocks.Count; index++)
        {
            if (written[index] == 0)
            {
                continue;
            }

            firstWrites[index] = firstWrite;

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

        return ([.. kept], firstWrites);
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

    private sealed class WriteRecorder(BlockIndex lookup, StackLookup stacks)
    {
        public MemoryHistoryHitCounts Counts { get; } = new();

        public int[] Written { get; } = new int[lookup.Blocks.Count];

        public MemoryHistoryWrite[] Writes { get; private set; } = [];

        public byte[] Data { get; private set; } = [];

        public Dictionary<int, ulong> Iterators { get; } = [];

        private int WriteCount { get; set; }

        private int DataLength { get; set; }

        private int[] Next { get; set; } = [];

        private int DataAt { get; set; }

        private uint[]? Highs { get; set; }

        private bool IsWriting { get; set; }

        private (int CallNodeIndex, int CallIndex, ulong Instructions)? Writer { get; set; }

        public void Count(ChunkHit hit, ReadOnlySpan<byte> bytes)
        {
            var first = WriteCount;

            var (isInBlock, isBufferPool) = Apply(hit, bytes);

            Counts.Add(WriteCount > first, isBufferPool, isInBlock);
        }

        public void BeginWriting(int[] firstWrites)
        {
            foreach (var block in lookup.Blocks)
            {
                if (block.Known is { } known)
                {
                    Array.Clear(known);
                }
            }

            Writes = new MemoryHistoryWrite[WriteCount];

            Data = new byte[DataLength];

            Next = firstWrites;

            IsWriting = true;
        }

        public void Write(ChunkHit hit, ReadOnlySpan<byte> bytes)
        {
            Writer = null;

            Apply(hit, bytes);
        }

        public HighWordRuns GetInstructionHighs() => Highs is null ? HighWordRuns.Empty : HighWordRuns.Build(Highs);

        private (bool IsInBlock, bool IsBufferPool) Apply(ChunkHit hit, ReadOnlySpan<byte> bytes)
        {
            var address = hit.Address;

            var isInBlock = false;

            var isBufferPool = false;

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

                isInBlock = true;

                isBufferPool |= block.IsBufferPool;

                if (!block.IsBufferPool)
                {
                    AddChanges(hit, found, (int)(address - block.Address), bytes[..length]);
                }

                address += (ulong)length;

                bytes = bytes[length..];
            }

            return (isInBlock, isBufferPool);
        }

        private void AddChanges(ChunkHit hit, int found, int offset, ReadOnlySpan<byte> bytes)
        {
            var block = lookup.Blocks[found];

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

                if (IsWriting)
                {
                    Add(hit, found, offset + start, run, type);
                }
                else
                {
                    Written[found]++;

                    WriteCount++;

                    DataLength += run.Length;
                }
            }
        }

        private void Add(ChunkHit hit, int block, int offset, ReadOnlySpan<byte> run, MemoryHistoryChangeType type)
        {
            var (callNodeIndex, callIndex, instructions) = Writer ??= GetWriter(hit);

            var target = Next[block]++;

            Writes[target] = new MemoryHistoryWrite(hit.Position,
                                                    (uint)instructions,
                                                    callNodeIndex,
                                                    callIndex,
                                                    offset,
                                                    DataAt,
                                                    (ushort)run.Length,
                                                    type);

            var high = (uint)(instructions >> 32);

            if (high != 0)
            {
                Highs ??= new uint[Writes.Length];

                Highs[target] = high;
            }

            run.CopyTo(Data.AsSpan(DataAt));

            DataAt += run.Length;
        }

        private (int CallNodeIndex, int CallIndex, ulong Instructions) GetWriter(ChunkHit hit)
        {
            var writer = stacks.GetWriter(hit.Thread, hit.Position);

            if (writer.CallNodeIndex >= 0 && stacks.GetIterator(writer.CallNodeIndex) is not 0 and var iterator)
            {
                Iterators[writer.CallNodeIndex] = iterator;
            }

            return writer;
        }
    }

    private sealed class HitOrder
    {
        private HitOrder(WatchResult watched, int[] starts, int[] next, int[] heads, (TracePosition Position, long Location)[] sorted)
        {
            Watched = watched;
            Starts = starts;
            Next = next;
            Heads = heads;
            Sorted = sorted;
        }

        private WatchResult Watched { get; }

        private int[] Starts { get; }

        private int[] Next { get; }

        private int[] Heads { get; }

        private (TracePosition Position, long Location)[] Sorted { get; }

        public static HitOrder Get(WatchResult watched) => IsOrdered(watched) ? new HitOrder(watched, [], [], [], []) : GetMerged(watched);

        public IEnumerable<(ChunkHit Hit, byte[] Data)> GetHits()
        {
            if (Sorted.Length > 0)
            {
                return GetSortedHits();
            }

            return Heads.Length > 0 ? GetMergedHits() : Watched.Chunks.SelectMany(c => c.Hits.Select(h => (Hit: h, c.Data)));
        }

        private static HitOrder GetMerged(WatchResult watched)
        {
            var chunks = watched.Chunks;

            var starts = new int[chunks.Count];

            var total = 0;

            for (var chunk = 0; chunk < chunks.Count; chunk++)
            {
                starts[chunk] = total;

                total += chunks[chunk].Hits.Length;
            }

            var next = new int[total];

            var lasts = new Dictionary<uint, (int Index, TracePosition Position)>();

            var heads = new List<int>();

            for (var chunk = 0; chunk < chunks.Count; chunk++)
            {
                var hits = chunks[chunk].Hits;

                for (var index = 0; index < hits.Length; index++)
                {
                    var hit = hits[index];

                    var global = starts[chunk] + index;

                    next[global] = -1;

                    if (lasts.TryGetValue(hit.Thread, out var last))
                    {
                        if (hit.Position < last.Position)
                        {
                            return GetSorted(watched);
                        }

                        next[last.Index] = global;
                    }
                    else
                    {
                        heads.Add(global);
                    }

                    lasts[hit.Thread] = (global, hit.Position);
                }
            }

            return new HitOrder(watched, starts, next, [.. heads], []);
        }

        private static HitOrder GetSorted(WatchResult watched)
        {
            var keys = new (TracePosition Position, long Location)[watched.HitCount];

            var at = 0;

            for (var chunk = 0; chunk < watched.Chunks.Count; chunk++)
            {
                var hits = watched.Chunks[chunk].Hits;

                for (var index = 0; index < hits.Length; index++)
                {
                    keys[at++] = (hits[index].Position, (long)chunk << 32 | (uint)index);
                }
            }

            Array.Sort(keys);

            return new HitOrder(watched, [], [], [], keys);
        }

        private static bool IsOrdered(WatchResult watched)
        {
            var last = default(TracePosition);

            foreach (var chunk in watched.Chunks)
            {
                foreach (var hit in chunk.Hits)
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

        private IEnumerable<(ChunkHit Hit, byte[] Data)> GetMergedHits()
        {
            var queue = new PriorityQueue<int, TracePosition>(Heads.Length);

            foreach (var head in Heads)
            {
                queue.Enqueue(head, GetHit(head).Hit.Position);
            }

            while (queue.TryDequeue(out var global, out _))
            {
                yield return GetHit(global);

                if (Next[global] is >= 0 and var following)
                {
                    queue.Enqueue(following, GetHit(following).Hit.Position);
                }
            }
        }

        private IEnumerable<(ChunkHit Hit, byte[] Data)> GetSortedHits()
        {
            foreach (var (_, location) in Sorted)
            {
                var chunk = Watched.Chunks[(int)(location >> 32)];

                yield return (chunk.Hits[(int)location], chunk.Data);
            }
        }

        private (ChunkHit Hit, byte[] Data) GetHit(int global)
        {
            var index = Array.BinarySearch(Starts, global);

            var chunk = index >= 0 ? index : ~index - 1;

            return (Watched.Chunks[chunk].Hits[global - Starts[chunk]], Watched.Chunks[chunk].Data);
        }
    }
}
