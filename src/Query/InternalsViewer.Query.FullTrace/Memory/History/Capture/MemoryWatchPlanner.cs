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
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.History.Capture;

internal static class MemoryWatchPlanner
{
    public const ulong MaximumBlockSize = 64 << 20;

    public static MemoryHistoryAllocation[] GetAllocations(TraceCallStore calls,
                                                           IEnumerable<AllocationFunction> functions,
                                                           MemoryBlock[]? buffers = null,
                                                           MemoryBlock[]? pages = null)
    {
        var events = new List<(TracePosition Position, ulong Pointer, ulong Bytes, uint Thread, bool IsClerkPage, bool IsArray)>();

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

                if (function.Operation is not (AllocationOperation.Allocate or AllocationOperation.AllocateArray)
                    && function.GetPointer(call) is not 0 and var released)
                {
                    events.Add((list.GetFromPosition(index), released, 0, thread, false, false));
                }

                if (function.IsAllocation
                    && call.ReturnedValue >= TraceAddress.MinimumAddress
                    && function.GetByteCount(call) is > 0 and <= MaximumBlockSize and var bytes)
                {
                    events.Add((list.GetToPosition(index),
                                call.ReturnedValue,
                                bytes,
                                thread,
                                function.CarriesClerk,
                                function.Operation == AllocationOperation.AllocateArray));
                }
            }
        }

        var live = new Dictionary<ulong, List<(ulong Bytes, uint Thread, TracePosition Start, bool IsClerkPage, bool IsArray)>>();

        var allocations = new List<MemoryHistoryAllocation>();

        foreach (var (position, pointer, bytes, thread, isClerkPage, isArray) in events.OrderBy(e => e.Position))
        {
            if (bytes == 0)
            {
                if (live.Remove(pointer, out var released))
                {
                    allocations.AddRange(released.Select(r => new MemoryHistoryAllocation(
                                                             new MemoryBlock(pointer, r.Bytes, r.Thread, r.Start, position),
                                                             r.IsClerkPage,
                                                             IsArray: r.IsArray)));
                }

                continue;
            }

            ref var open = ref CollectionsMarshal.GetValueRefOrAddDefault(live, pointer, out _);

            open ??= [];

            if (!open.Exists(o => o.Bytes == bytes))
            {
                open.Add((bytes, thread, position, isClerkPage, isArray));
            }
        }

        foreach (var (pointer, open) in live)
        {
            allocations.AddRange(open.Select(o => new MemoryHistoryAllocation(
                                                     new MemoryBlock(pointer, o.Bytes, o.Thread, o.Start, TracePosition.Unknown),
                                                     o.IsClerkPage,
                                                     IsArray: o.IsArray)));
        }

        allocations.AddRange((buffers ?? []).Select(b => new MemoryHistoryAllocation(b, IsClerkPage: false, IsBuffer: true)));

        var allocated = allocations.Select(a => a.Block).ToArray();

        allocations.AddRange((pages ?? []).Where(p => !allocated.Any(a => a.Address <= p.Address
                                                                          && p.Address + p.Size <= a.Address + a.Size))
                             .Select(p => new MemoryHistoryAllocation(p, IsClerkPage: false)));

        return [.. allocations.OrderBy(a => a.Block.FromPosition).ThenBy(a => a.Block.Address)];
    }

    public static WatchRange[] GetRanges(IEnumerable<MemoryBlock> blocks, IEnumerable<WatchRange> others)
    {
        var ranges = blocks.Select(b => new WatchRange(b.Address, b.Size)).Concat(others);

        return [.. IntervalHelper.Coalesce(ranges, r => r.Address, r => r.Address + r.Size)
                                 .Select(r => new WatchRange(r.Start, r.End - r.Start))];
    }

    public static Func<WatchHit, bool> GetWatchFilter(IReadOnlyList<MemoryHistoryAllocation> allocations)
    {
        var index = new WatchedBlockIndex([.. allocations.Select(a => new WatchedBlock(0,
                                                                                       a.Block.Address,
                                                                                       a.Block.Size,
                                                                                       isIterator: false,
                                                                                       isInline: false,
                                                                                       a.IsClerkPage,
                                                                                       a.Block.Thread,
                                                                                       a.Block.FromPosition,
                                                                                       a.Block.ToPosition))]);

        return hit => index.IsLive(hit.Address, hit.Size, hit.Position);
    }

    public static Dictionary<ulong, TracePosition> GetFirstPositions(TraceCallStore calls, IReadOnlySet<ulong> iterators)
    {
        var positions = new Dictionary<ulong, TracePosition>();

        foreach (var list in calls.GetCallLists())
        {
            if (!iterators.Contains(list.InstanceAddress) || !IteratorSymbolLookup.IsIterator(calls.GetFirstNode(list)?.Frame))
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

    private static bool IsWatched(AllocationFunction function)
        => !function.IsVirtual
           && (function.IsAllocation || function is { Operation: AllocationOperation.Free, PointerArgumentSlot: >= 0 });
}
