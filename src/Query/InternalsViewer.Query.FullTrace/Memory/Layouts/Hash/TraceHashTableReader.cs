using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.History;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

public static class TraceHashTableReader
{
    private const string InsertMethod = "Insert";

    private const string LookupMethod = "Lookup";

    private const int GroupArgument = 0;

    private const int HashArgument = 2;

    private const int DirectoryShift = 16;

    private const int PageShift = 6;

    private const uint PageIndexMask = 0x3FF;

    private const int MaximumPartitions = 256;

    public static TraceHashTable? Read(MemoryHistoryStore store, ulong group, TracePosition position)
    {
        var memory = new MemoryHistoryReader(store, position);

        if (!memory.TryRead(group + TraceHashTableLayout.IteratorOffset, out ulong iterator))
        {
            return null;
        }

        memory.TryRead(group + TraceHashTableLayout.ActivePartitionOffset, out ulong active);

        var partitions = new List<TraceHashPartition>();

        var head = group + TraceHashTableLayout.PartitionListOffset;

        var isLinked = memory.TryRead(head + sizeof(ulong), out ulong link);

        while (isLinked && link != 0 && link != head && partitions.Count < MaximumPartitions)
        {
            partitions.Add(ReadPartition(memory, partitions.Count, link - TraceHashTableLayout.PartitionLinkOffset));

            isLinked = memory.TryRead(link + sizeof(ulong), out link);
        }

        memory.TryRead(iterator + TraceHashTableLayout.PartitionMaskOffset, out uint partitionMask);

        memory.TryRead(iterator + TraceHashTableLayout.PartitionShiftOffset, out uint partitionShift);

        return new TraceHashTable(group, iterator, active, partitionMask, (int)partitionShift, partitions);
    }

    public static List<TraceHashAccess> GetAccesses(TraceCallStore calls, ulong group)
    {
        var accesses = new List<TraceHashAccess>();

        foreach (var (list, isInsert) in GetGroupCalls(calls))
        {
            for (var index = 0; index < list.Count; index++)
            {
                var call = list[index];

                if (call.GetSlot(GroupArgument) == group)
                {
                    var hash = (uint)call.GetSlot(HashArgument);

                    accesses.Add(new TraceHashAccess(list.GetFromPosition(index), hash, isInsert, call.ReturnedValue != 0));
                }
            }
        }

        accesses.Sort((left, right) => left.Position.CompareTo(right.Position));

        return accesses;
    }

    private static IEnumerable<(TraceCallList List, bool IsInsert)> GetGroupCalls(TraceCallStore calls)
    {
        foreach (var list in calls.GetCallLists())
        {
            if (calls.GetFirstNode(list)?.Frame?.Function is { ClassName: TraceHashTableLayout.GroupClass } function
                && function.MethodName is InsertMethod or LookupMethod)
            {
                yield return (list, function.MethodName == InsertMethod);
            }
        }
    }

    private static TraceHashPartition ReadPartition(MemoryHistoryReader memory, int index, ulong instance)
    {
        memory.TryRead(instance + TraceHashTableLayout.RowCountOffset, out uint rows);

        memory.TryRead(instance + TraceHashTableLayout.BucketMaskOffset, out uint mask);

        memory.TryRead(instance + TraceHashTableLayout.DirectoryOffset, out ulong directory);

        memory.TryRead(instance + TraceHashTableLayout.DirectoryCountOffset, out uint directoryCount);

        var pages = new List<TraceHashPage>();

        var buckets = new List<TraceHashBucket>();

        for (var first = 0L; directory != 0 && directoryCount > 0 && first <= mask; first += TraceHashTableLayout.BucketsPerPage)
        {
            var entry = (ulong)((first >> DirectoryShift) & (directoryCount - 1));

            var pointerIndex = (int)((ulong)(first >> PageShift) & PageIndexMask);

            if (!memory.TryRead(directory + entry * sizeof(ulong), out ulong pointerPage) || pointerPage == 0)
            {
                continue;
            }

            memory.TryRead(pointerPage + (ulong)pointerIndex * sizeof(ulong), out ulong page);

            pages.Add(new TraceHashPage(pointerPage, pointerIndex, page, (int)first));

            if (page == 0)
            {
                continue;
            }

            var count = Math.Min(TraceHashTableLayout.BucketsPerPage, mask + 1 - first);

            for (var offset = 0; offset < count; offset++)
            {
                var address = page + (ulong)(offset * TraceHashTableLayout.BucketSize);

                if (!memory.TryRead(address, out byte rowCount) || rowCount == 0)
                {
                    continue;
                }

                memory.TryRead(address + TraceHashTableLayout.KeyOffset, out uint key);

                memory.TryRead(address + TraceHashTableLayout.RowOffset, out ulong row);

                buckets.Add(new TraceHashBucket((int)(first + offset), address, rowCount, key, row));
            }
        }

        return new TraceHashPartition(index, instance, (int)rows, mask, directory, (int)directoryCount, pages, buckets);
    }
}
