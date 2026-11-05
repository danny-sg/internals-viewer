using InternalsViewer.Query.FullTrace.Memory.Allocation;
using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory.Allocation.Enums;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.FullTrace.Memory.History.Capture.StackObjects;
using InternalsViewer.Query.FullTrace.Memory.History.Capture.Watches;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.History.Capture;

internal static class MemoryHistoryBuilder
{
    public const int MaximumIteratorSize = 0x2000;

    public const ulong MaximumBlockSize = 64 << 20;

    private const int PageShift = 12;

    private const ulong PageSize = 1ul << PageShift;

    public static MemoryHistoryAllocation[] GetAllocations(TraceCallStore calls,
                                                           IEnumerable<AllocationFunction> functions,
                                                           MemoryBlock[]? buffers = null,
                                                           MemoryBlock[]? pages = null)
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

        allocations.AddRange((buffers ?? []).Select(b => new MemoryHistoryAllocation(b, IsClerkPage: false, IsBuffer: true)));

        var allocated = allocations.Select(a => a.Block).ToArray();

        allocations.AddRange((pages ?? []).Where(p => !allocated.Any(a => a.Pointer <= p.Pointer && p.Pointer + p.Bytes <= a.Pointer + a.Bytes))
                                          .Select(p => new MemoryHistoryAllocation(p, IsClerkPage: false)));

        return [.. allocations.OrderBy(a => a.Block.Start).ThenBy(a => a.Block.Pointer)];
    }

    public static WatchRange[] GetRanges(IEnumerable<MemoryBlock> blocks) => GetRanges(blocks, []);

    public static WatchRange[] GetRanges(IEnumerable<MemoryBlock> blocks, IEnumerable<WatchRange> others)
    {
        var ranges = blocks.Select(b => new WatchRange(b.Pointer, b.Bytes)).Concat(others);

        return [.. IntervalHelper.Coalesce(ranges, r => r.Address, r => r.Address + r.Size).Select(r => new WatchRange(r.Start, r.End - r.Start))];
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
                                                                              a.Block.End))]);

        return hit => index.IsLive(hit.Address, hit.Size, hit.Position);
    }

    public static Dictionary<ulong, TracePosition> GetFirstPositions(TraceCallStore calls, IReadOnlySet<ulong> iterators)
    {
        var positions = new Dictionary<ulong, TracePosition>();

        foreach (var list in calls.GetCallLists())
        {
            if (!iterators.Contains(list.InstanceAddress))
            {
                continue;
            }

            var position = list.GetFromPosition(0);

            if (!positions.TryGetValue(list.InstanceAddress, out var first) || position < first)
            {
                positions[list.InstanceAddress] = position;
            }
        }

        return positions;
    }

    public static (MemoryHistoryStore Store, MemoryHistoryCaptureCounter Counts) Build(IReadOnlyList<MemoryHistoryAllocation> allocations,
                                                                                  IReadOnlyList<ulong> owners,
                                                                                  IReadOnlyDictionary<ulong, TracePosition> iterators,
                                                                                  WatchResult watched,
                                                                                  TraceTimeline? timeline,
                                                                                  StackSnapshotResult? stack = null)
        => new Recording(allocations, owners, iterators, timeline, stack).Build(watched);

    private static bool IsWatched(AllocationFunction function)
        => !function.IsVirtual
           && (function.IsAllocation || function is { Operation: AllocationOperation.Free, PointerArgumentSlot: >= 0 });

    private static MemoryHistoryChangeType? GetChangeType(WatchedBlock block, int offset, byte value)
    {
        if (!block.Known![offset])
        {
            return MemoryHistoryChangeType.Initialised;
        }

        return block.Image![offset] != value ? MemoryHistoryChangeType.Changed : null;
    }

    private static (MemoryHistoryBlock[] Kept, int[] FirstWrites, MemoryHistoryInstance[] Instances)
        GetKeptBlocks(List<WatchedBlock> blocks, StackLookup stacks, int[] written, bool[] isKept)
    {
        var firstWrites = new int[blocks.Count];

        var kept = new List<MemoryHistoryBlock>();

        var instances = new List<MemoryHistoryInstance>();

        var firstWrite = 0;

        for (var index = 0; index < blocks.Count; index++)
        {
            if (!isKept[index])
            {
                continue;
            }

            firstWrites[index] = firstWrite;

            var block = blocks[index];

            var callNodeIndex = block.IsStack ? block.CallNodeIndex : stacks.GetCallNodeIndex(block.Thread, block.FromPosition);

            var firstInstance = instances.Count;

            foreach (var (from, to) in block.Instances.OrderBy(i => i.From))
            {
                instances.Add(new MemoryHistoryInstance(from,
                                                        to,
                                                        stacks.GetInstructions(block.Thread, from),
                                                        to.IsKnown ? stacks.GetInstructions(block.Thread, to) : 0));
            }

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
                                            written[index])
            {
                IsStack = block.IsStack,
                FirstInstance = firstInstance,
                InstanceCount = block.Instances.Count
            });

            firstWrite += written[index];
        }

        return ([.. kept], firstWrites, [.. instances]);
    }

    internal sealed class Recording
    {
        internal Recording(IReadOnlyList<MemoryHistoryAllocation> allocations,
                           IReadOnlyList<ulong> owners,
                           IReadOnlyDictionary<ulong, TracePosition> iterators,
                           TraceTimeline? timeline,
                           StackSnapshotResult? stack = null)
        {
            Stacks = new StackLookup(timeline);

            Timeline = timeline;

            Stack = stack ?? StackSnapshotResult.Empty;

            for (var index = 0; index < allocations.Count; index++)
            {
                var allocation = allocations[index].Block;

                Blocks.Add(new WatchedBlock(owners[index],
                                            allocation.Pointer,
                                            allocation.Bytes,
                                            owners[index] == allocation.Pointer,
                                            isInline: false,
                                            allocations[index].IsClerkPage,
                                            allocation.Thread,
                                            allocation.Start,
                                            allocation.End,
                                            allocations[index].IsBuffer));
            }

            Sites = AddStackSites();

            Lookup = new BlockIndex(Blocks);

            Lookup.ClipToContainers();

            Lookup.AddIterators(iterators);

            foreach (var block in Blocks)
            {
                block.IsBufferPool = block is { IsIterator: false, IsBuffer: false, IsStack: false }
                                     && Stacks.IsBufferPool(Stacks.GetCallNodeIndex(block.Thread, block.FromPosition), block.IsClerkPage);
            }

            Reads = new ReadRecorder(Lookup, timeline);
        }

        public MemoryHistoryCaptureCounter ReadCounts => Reads.Counts;

        private List<WatchedBlock> Blocks { get; } = [];

        private BlockIndex Lookup { get; }

        private StackLookup Stacks { get; }

        private TraceTimeline? Timeline { get; }

        private ReadRecorder Reads { get; }

        private StackSnapshotResult Stack { get; }

        private int[] Sites { get; }

        public void AddRead(WatchHit hit) => Reads.Add(hit);

        public bool IsObjectMemory(WatchHit hit)
            => Lookup.GetInnermost(hit.Address, hit.Position) is { } found && !Lookup.Blocks[found].IsBufferPool;

        public (MemoryHistoryStore Store, MemoryHistoryCaptureCounter Counts) Build(WatchResult watched)
        {
            var recorder = new WriteRecorder(Lookup, Stacks);

            var order = HitOrder.Get(watched);

            foreach (var (hit, bytes) in order.GetHits())
            {
                recorder.Count(hit, bytes.AsSpan(hit.DataOffset, hit.Size));
            }

            ApplySnapshots(recorder);

            Reads.Close();

            var isKept = Reads.GetReadBlocks(Blocks.Count);

            for (var index = 0; index < isKept.Length; index++)
            {
                isKept[index] |= recorder.Written[index] > 0;
            }

            var (kept, firstWrites, instances) = GetKeptBlocks(Blocks, Stacks, recorder.Written, isKept);

            recorder.BeginWriting(firstWrites);

            foreach (var (hit, bytes) in order.GetHits())
            {
                recorder.Write(hit, bytes.AsSpan(hit.DataOffset, hit.Size));
            }

            ApplySnapshots(recorder);

            var (reads, readStarts) = Reads.Build(isKept, kept.Length);

            var store = new MemoryHistoryStore(kept,
                                               recorder.Writes,
                                               recorder.Data,
                                               recorder.Iterators,
                                               Timeline,
                                               reads,
                                               readStarts,
                                               instances);

            return (store, recorder.Counts);
        }

        private void ApplySnapshots(WriteRecorder recorder)
        {
            foreach (var chunk in Stack.Chunks)
            {
                foreach (var run in chunk.Hits)
                {
                    var item = Stack.Objects[run.Object];

                    var hit = new ChunkHit(run.Position, item.Address + (ulong)run.Offset, run.DataOffset, item.Thread, run.Length);

                    recorder.ApplySnapshot(Sites[run.Object], hit, chunk.Data.AsSpan(run.DataOffset, run.Length));
                }
            }
        }

        private int[] AddStackSites()
        {
            var sites = new Dictionary<(uint Thread, int Owner, ulong Address, int Size), int>();

            var blocks = new int[Stack.Objects.Length];

            for (var index = 0; index < Stack.Objects.Length; index++)
            {
                var item = Stack.Objects[index];

                var from = Stacks.GetFrameStart(item.Thread, item.FirstPosition, item.OwnerCallNodeIndex) ?? item.FirstPosition;

                if (!sites.TryGetValue((item.Thread, item.OwnerCallNodeIndex, item.Address, item.Size), out var site))
                {
                    site = Blocks.Count;

                    sites[(item.Thread, item.OwnerCallNodeIndex, item.Address, item.Size)] = site;

                    Blocks.Add(new WatchedBlock(0,
                                                item.Address,
                                                (ulong)item.Size,
                                                isIterator: false,
                                                isInline: false,
                                                isClerkPage: false,
                                                item.Thread,
                                                from,
                                                item.EndPosition,
                                                isStack: true,
                                                callNodeIndex: item.OwnerCallNodeIndex));
                }

                Blocks[site].AddInstance(from, item.EndPosition);

                blocks[index] = site;
            }

            return blocks;
        }
    }

    private sealed class ReadRecorder(BlockIndex lookup, TraceTimeline? timeline)
    {
        private const int FirstSweep = 64;

        public MemoryHistoryCaptureCounter Counts { get; } = new();

        private Dictionary<uint, ThreadMarkTable> Threads { get; } = [];

        private ChunkedList<ClosedMark> Closed { get; set; } = [];

        private TraceInnermostCallLookup Calls { get; } = new(timeline);

        private bool IsOrdered { get; set; } = true;

        public void Add(WatchHit hit)
        {
            Counts.Received++;

            var position = hit.Position;

            if (lookup.GetInnermost(hit.Address, position) is not { } found)
            {
                Counts.Outside++;

                return;
            }

            var block = lookup.Blocks[found];

            if (block.IsBufferPool)
            {
                Counts.BufferPool++;

                return;
            }

            var spanId = Calls.GetSpanId(hit.Thread, position, out var spanEnd);

            var offset = (int)(hit.Address - block.Address);

            var end = (int)Math.Min(block.Size, hit.Address - block.Address + hit.Size);

            var table = GetTable(hit.Thread);

            if (position < table.Latest)
            {
                IsOrdered = false;
            }
            else
            {
                table.Latest = position;
            }

            ref var mark = ref CollectionsMarshal.GetValueRefOrAddDefault(table.Open, (found, spanId), out var exists);

            mark = exists
                ? mark with { Mark = mark.Mark.Add(position, offset, end) }
                : new OpenMark(new ReadMark(position, offset, end, 1), spanEnd);

            Counts.Recorded++;

            if (table.Open.Count >= table.SweepAt)
            {
                Sweep(table);
            }
        }

        public void Close()
        {
            foreach (var table in Threads.Values)
            {
                foreach (var ((block, spanId), open) in table.Open)
                {
                    Closed.Add(new ClosedMark(block, spanId, open.Mark));
                }
            }

            Threads.Clear();

            if (!IsOrdered)
            {
                MergeRepeats();
            }
        }

        public bool[] GetReadBlocks(int count)
        {
            var blocks = new bool[count];

            for (var index = 0; index < Closed.Count; index++)
            {
                if (Closed[index].Block >= 0)
                {
                    blocks[Closed[index].Block] = true;
                }
            }

            return blocks;
        }

        public (MemoryHistoryReadEntry[] Reads, int[] Starts) Build(bool[] isKept, int keptCount)
        {
            var kept = new int[isKept.Length];

            var next = 0;

            for (var index = 0; index < isKept.Length; index++)
            {
                kept[index] = isKept[index] ? next++ : -1;
            }

            var starts = new int[keptCount + 1];

            for (var index = 0; index < Closed.Count; index++)
            {
                if (Closed[index].Block >= 0 && kept[Closed[index].Block] >= 0)
                {
                    starts[kept[Closed[index].Block] + 1]++;
                }
            }

            for (var index = 1; index < starts.Length; index++)
            {
                starts[index] += starts[index - 1];
            }

            var reads = new MemoryHistoryReadEntry[starts[^1]];

            var placed = new int[keptCount];

            for (var index = 0; index < Closed.Count; index++)
            {
                var (block, spanId, mark) = Closed[index];

                if (block < 0)
                {
                    continue;
                }

                if (kept[block] < 0)
                {
                    Counts.Unwritten += mark.Count;

                    continue;
                }

                var target = starts[kept[block]] + placed[kept[block]]++;

                reads[target] = new MemoryHistoryReadEntry(mark.First, spanId, mark.Start, mark.End - mark.Start, mark.Count);
            }

            Closed = [];

            for (var block = 0; block < keptCount; block++)
            {
                reads.AsSpan(starts[block], starts[block + 1] - starts[block])
                     .Sort(static (left, right) => left.Position.CompareTo(right.Position));
            }

            return (reads, starts);
        }

        private ThreadMarkTable GetTable(uint thread)
        {
            if (!Threads.TryGetValue(thread, out var table))
            {
                table = new ThreadMarkTable { SweepAt = FirstSweep };

                Threads[thread] = table;
            }

            return table;
        }

        private void Sweep(ThreadMarkTable table)
        {
            foreach (var ((block, spanId), open) in table.Open)
            {
                if (open.End.IsKnown && open.End <= table.Latest)
                {
                    Closed.Add(new ClosedMark(block, spanId, open.Mark));

                    table.Open.Remove((block, spanId));
                }
            }

            table.SweepAt = Math.Max(FirstSweep, table.Open.Count * 2);
        }

        private void MergeRepeats()
        {
            var firsts = new Dictionary<(int Block, int SpanId), int>(Closed.Count);

            for (var index = 0; index < Closed.Count; index++)
            {
                ref var repeat = ref Closed[index];

                if (!firsts.TryAdd((repeat.Block, repeat.SpanId), index))
                {
                    ref var first = ref Closed[firsts[(repeat.Block, repeat.SpanId)]];

                    first = first with { Mark = first.Mark.Merge(repeat.Mark) };

                    repeat = repeat with { Block = -1 };
                }
            }
        }
    }

    private readonly record struct ReadMark(TracePosition First, int Start, int End, int Count)
    {
        public ReadMark Add(TracePosition position, int start, int end)
            => new(position < First ? position : First, Math.Min(start, Start), Math.Max(end, End), Count + 1);

        public ReadMark Merge(ReadMark other)
            => new(other.First < First ? other.First : First, Math.Min(other.Start, Start), Math.Max(other.End, End), Count + other.Count);
    }

    private readonly record struct OpenMark(ReadMark Mark, TracePosition End);

    private readonly record struct ClosedMark(int Block, int SpanId, ReadMark Mark);

    private sealed class ThreadMarkTable
    {
        public Dictionary<(int Block, int SpanId), OpenMark> Open { get; } = [];

        public TracePosition Latest { get; set; }

        public int SweepAt { get; set; }
    }

    private sealed class StackLookup(TraceTimeline? timeline)
    {
        private Dictionary<int, ulong> Iterators { get; } = [];

        private Dictionary<int, (bool IsBufferPool, bool IsGrant)> PageSources { get; } = [];

        private TraceInnermostCallLookup Calls { get; } = new(timeline);

        public int GetWriter(uint thread, TracePosition position) => Calls.GetSpanId(thread, position);

        public int GetCallNodeIndex(int spanId) => timeline?.GetSpanCall(spanId).CallNodeIndex ?? -1;

        public ulong GetInstructions(uint thread, TracePosition position)
            => timeline?.GetInnermostCall(thread, position) is { } span ? timeline.GetInstructions(span, position) : 0;

        public int GetCallNodeIndex(uint thread, TracePosition position)
            => timeline?.GetInnermostCall(thread, position) is { } span ? span.CallNodeIndex : -1;

        public TracePosition? GetFrameStart(uint thread, TracePosition position, int callNodeIndex)
        {
            foreach (var lane in timeline?.Threads ?? [])
            {
                if (lane.ThreadId != thread)
                {
                    continue;
                }

                for (var row = lane.Rows.Count - 1; row >= 0; row--)
                {
                    if (lane.Rows[row].GetIndex(position) is >= 0 and var index && lane.Rows[row].GetCallNodeIndex(index) == callNodeIndex)
                    {
                        return lane.Rows[row].GetFromPosition(index);
                    }
                }
            }

            return null;
        }

        public bool IsBufferPool(int callNodeIndex, bool isClerkPage)
        {
            if (!PageSources.TryGetValue(callNodeIndex, out var source))
            {
                CallStackNode[] ancestors = [.. timeline?.NodeOf(callNodeIndex)?.Ancestors() ?? []];

                source = (ancestors.Any(n => BufferPoolFunctionLookup.IsBufferPool(n.Frame)),
                          ancestors.Any(n => AllocationFunction.IsGrantFrame(n.Frame)));

                PageSources[callNodeIndex] = source;
            }

            return !source.IsGrant && (isClerkPage || source.IsBufferPool);
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
            => node.Frame is { InstanceAddress: not 0 } frame && IteratorSymbolLookup.IsIterator(frame);
    }

    private sealed class WatchedBlock(ulong iterator,
                                      ulong address,
                                      ulong size,
                                      bool isIterator,
                                      bool isInline,
                                      bool isClerkPage,
                                      uint thread,
                                      TracePosition fromPosition,
                                      TracePosition toPosition,
                                      bool isBuffer = false,
                                      bool isStack = false,
                                      int callNodeIndex = -1)
    {
        public ulong Iterator { get; } = iterator;

        public ulong Address { get; } = address;

        public ulong Size { get; } = size;

        public bool IsIterator { get; } = isIterator;

        public bool IsInline { get; } = isInline;

        public bool IsClerkPage { get; } = isClerkPage;

        public bool IsBuffer { get; } = isBuffer;

        public bool IsStack { get; } = isStack;

        public int CallNodeIndex { get; } = callNodeIndex;

        public List<(TracePosition From, TracePosition To)> Instances { get; } = [];

        public uint Thread { get; } = thread;

        public TracePosition FromPosition { get; } = fromPosition;

        public TracePosition ToPosition { get; set; } = toPosition;

        public bool IsBufferPool { get; set; }

        public byte[]? Image { get; set; }

        public bool[]? Known { get; set; }

        public bool Contains(ulong address, TracePosition position)
            => address >= Address && address - Address < Size && FromPosition <= position && position < ToPosition;

        public void AddInstance(TracePosition from, TracePosition to)
        {
            Instances.Add((from, to));

            if (ToPosition < to)
            {
                ToPosition = to;
            }
        }
    }

    private sealed class BlockIndex
    {
        public BlockIndex(List<WatchedBlock> blocks)
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
        public MemoryHistoryCaptureCounter Counts { get; } = new();

        public int[] Written { get; } = new int[lookup.Blocks.Count];

        public MemoryHistoryWriteEntry[] Writes { get; private set; } = [];

        public byte[] Data { get; private set; } = [];

        public Dictionary<int, ulong> Iterators { get; } = [];

        private int WriteCount { get; set; }

        private int DataLength { get; set; }

        private int[] WrittenBytes { get; } = new int[lookup.Blocks.Count];

        private int[] Next { get; set; } = [];

        private int[] NextData { get; set; } = [];

        private bool IsWriting { get; set; }

        private int? Writer { get; set; }

        private (ChunkHit Hit, int Block, int End, int Length)? LastInitialised { get; set; }

        private int? LastWriter { get; set; }

        public void Count(ChunkHit hit, ReadOnlySpan<byte> bytes)
        {
            Writer = null;

            var first = DataLength;

            var (isInBlock, isBufferPool) = Apply(hit, bytes);

            Counts.Add(DataLength > first, isBufferPool, isInBlock);
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

            Writes = new MemoryHistoryWriteEntry[WriteCount];

            Data = new byte[DataLength];

            Next = firstWrites;

            NextData = new int[WrittenBytes.Length];

            var dataStart = 0;

            for (var index = 0; index < WrittenBytes.Length; index++)
            {
                NextData[index] = dataStart;

                dataStart += WrittenBytes[index];
            }

            LastInitialised = null;

            LastWriter = null;

            IsWriting = true;
        }

        public void Write(ChunkHit hit, ReadOnlySpan<byte> bytes)
        {
            Writer = null;

            Apply(hit, bytes);
        }

        public void ApplySnapshot(int block, ChunkHit hit, ReadOnlySpan<byte> bytes)
        {
            Writer = null;

            AddChanges(hit, block, (int)(hit.Address - lookup.Blocks[block].Address), bytes);
        }

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

                var isContinuation = IsContinuation(hit, found, offset + start, run.Length, type);

                if (IsWriting && isContinuation)
                {
                    Extend(found, run);
                }
                else if (IsWriting)
                {
                    Add(hit, found, offset + start, run, type);
                }
                else
                {
                    if (!isContinuation)
                    {
                        Written[found]++;

                        WriteCount++;
                    }

                    DataLength += run.Length;

                    WrittenBytes[found] += run.Length;
                }

                Remember(hit, found, offset + start, run.Length, type, isContinuation);
            }
        }

        private bool IsContinuation(ChunkHit hit, int block, int offset, int length, MemoryHistoryChangeType type)
        {
            if (type != MemoryHistoryChangeType.Initialised
                || LastInitialised is not { } last
                || last.Block != block
                || last.End != offset
                || last.Hit.Thread != hit.Thread
                || last.Length + length > ushort.MaxValue)
            {
                return false;
            }

            var writer = Writer ??= GetWriter(hit);

            var previous = LastWriter ??= GetWriter(last.Hit);

            return writer >= 0 && writer == previous;
        }

        private void Remember(ChunkHit hit, int block, int offset, int length, MemoryHistoryChangeType type, bool isContinuation)
        {
            if (type != MemoryHistoryChangeType.Initialised)
            {
                LastInitialised = null;

                return;
            }

            if (isContinuation && LastInitialised is { } last)
            {
                LastInitialised = (last.Hit, block, offset + length, last.Length + length);

                return;
            }

            LastInitialised = (hit, block, offset + length, length);

            LastWriter = Writer;
        }

        private void Extend(int block, ReadOnlySpan<byte> run)
        {
            var target = Next[block] - 1;

            Writes[target] = Writes[target] with { Length = (ushort)(Writes[target].Length + run.Length) };

            AddData(block, run);
        }

        private void Add(ChunkHit hit, int block, int offset, ReadOnlySpan<byte> run, MemoryHistoryChangeType type)
        {
            var spanId = Writer ??= GetWriter(hit);

            Writes[Next[block]++] = new MemoryHistoryWriteEntry(hit.Position, spanId, offset, (ushort)run.Length, type);

            AddData(block, run);
        }

        private void AddData(int block, ReadOnlySpan<byte> run)
        {
            run.CopyTo(Data.AsSpan(NextData[block]));

            NextData[block] += run.Length;
        }

        private int GetWriter(ChunkHit hit)
        {
            var spanId = stacks.GetWriter(hit.Thread, hit.Position);

            if (stacks.GetCallNodeIndex(spanId) is >= 0 and var callNodeIndex && stacks.GetIterator(callNodeIndex) is not 0 and var iterator)
            {
                Iterators[callNodeIndex] = iterator;
            }

            return spanId;
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
