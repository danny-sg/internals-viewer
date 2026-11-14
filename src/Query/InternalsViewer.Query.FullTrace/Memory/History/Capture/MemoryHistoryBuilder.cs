using InternalsViewer.Query.FullTrace.Memory.Allocation;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory.Allocation.Enums;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.FullTrace.Memory.History.Capture.StackObjects;
using InternalsViewer.Query.FullTrace.Memory.History.Capture.Watches;
using InternalsViewer.Query.FullTrace.Memory.MemoryObject;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.History.Capture;

internal sealed class MemoryHistoryBuilder
{
    internal MemoryHistoryBuilder(IReadOnlyList<MemoryHistoryAllocation> allocations,
                                  IReadOnlyList<ulong> owners,
                                  IReadOnlyDictionary<ulong, TracePosition> iterators,
                                  TraceTimeline? timeline,
                                  StackSnapshotResult? stack = null,
                                  Func<ulong, TracePosition, MemoryObjectReference?>? getPageOwner = null)
    {
        Stacks = new WatchHitCallResolver(timeline);

        Timeline = timeline;

        Stack = stack ?? StackSnapshotResult.Empty;

        for (var index = 0; index < allocations.Count; index++)
        {
            var allocation = allocations[index].Block;

            Blocks.Add(new WatchedBlock(owners[index],
                                        allocation.Address,
                                        allocation.Size,
                                        owners[index] == allocation.Address,
                                        isInline: false,
                                        allocations[index].IsClerkPage,
                                        allocation.Thread,
                                        allocation.FromPosition,
                                        allocation.ToPosition,
                                        allocations[index].IsBuffer,
                                        isArray: allocations[index].IsArray));
        }

        Sites = AddStackSites();

        Lookup = new WatchedBlockIndex(Blocks);

        Lookup.EndReusedBlocks(getPageOwner);

        Lookup.ClipToContainers();

        Lookup.AddIterators(iterators);

        foreach (var block in Blocks)
        {
            block.IsBufferPool = block is { IsIterator: false, IsBuffer: false, IsStack: false }
                                 && Stacks.IsBufferPool(Stacks.GetCallNodeIndex(block.Thread, block.FromPosition), block.IsClerkPage);
        }

        Reads = new MemoryHistoryReadBuilder(Lookup, timeline);
    }

    public MemoryHistoryCaptureCounter ReadCounts => Reads.Counts;

    private List<WatchedBlock> Blocks { get; } = [];

    private WatchedBlockIndex Lookup { get; }

    private WatchHitCallResolver Stacks { get; }

    private TraceTimeline? Timeline { get; }

    private MemoryHistoryReadBuilder Reads { get; }

    private StackSnapshotResult Stack { get; }

    private int[] Sites { get; }

    public void AddRead(WatchHit hit) => Reads.Add(hit);

    public bool IsObjectMemory(WatchHit hit)
        => Lookup.GetInnermost(hit.Address, hit.Position) is { } found && !Lookup.Blocks[found].IsBufferPool;

    public (MemoryHistoryStore Store, MemoryHistoryCaptureCounter Counts) Build(WatchResult watched)
    {
        var recorder = new MemoryHistoryWriteBuilder(Lookup, Stacks);

        var order = WatchHitOrder.Get(watched);

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

        var writeTable = MemoryHistoryAccessTable.Build(recorder.Writes, recorder.Data, MemoryHistoryStore.WriteCacheSlots);

        var readTable = MemoryHistoryAccessTable.Build(reads, [], MemoryHistoryStore.ReadCacheSlots);

        var store = new MemoryHistoryStore(kept, writeTable, recorder.Iterators, Timeline, readTable, readStarts, instances);

        return (store, recorder.Counts);
    }

    private void ApplySnapshots(MemoryHistoryWriteBuilder recorder)
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

    private static (MemoryHistoryBlock[] Kept, int[] FirstWrites, MemoryHistoryInstance[] Instances)
        GetKeptBlocks(List<WatchedBlock> blocks, WatchHitCallResolver stacks, int[] written, bool[] isKept)
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
                IsArray = block.IsArray,
                FirstInstance = firstInstance,
                InstanceCount = block.Instances.Count
            });

            firstWrite += written[index];
        }

        return ([.. kept], firstWrites, [.. instances]);
    }
}
