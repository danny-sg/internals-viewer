using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Pages;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash.Enums;

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

    private const int MaximumItems = 1024;

    private const int MaximumAllocatorDepth = 6;

    private const string BucketPageAllocator = "PvAllocateHashBucketPage";

    private const string IteratorClass = "CQScanHash";

    private const string GroupClass = "CHashWorkfilePartitionGroup";

    private const string WorktableGroupClass = "CHashWorktablePartitionGroup";

    private const string PartitionClass = "CHashWorkfilePartitionInstance";

    private const string OtherBucketPagesName = "Other Bucket Pages";

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
            partitions.Add(ReadPartition(store, memory, position, partitions.Count, link - TraceHashTableLayout.PartitionLinkOffset));

            isLinked = memory.TryRead(link + sizeof(ulong), out link);
        }

        memory.TryRead(iterator + TraceHashTableLayout.PartitionMaskOffset, out uint partitionMask);

        memory.TryRead(iterator + TraceHashTableLayout.PartitionShiftOffset, out uint partitionShift);

        var (columnArray, columns) = ReadColumns(store, memory, position, group);

        return new TraceHashTable(group, iterator, active, partitionMask, (int)partitionShift, partitions)
        {
            ColumnArray = columnArray,
            Columns = columns
        };
    }

    public static List<TraceMemoryUsage> GetMemory(MemoryHistoryStore store, AddressMap addressMap, TraceCallStore calls, ulong iterator)
    {
        var parts = new Dictionary<int, string>();

        foreach (var group in GetGroups(calls))
        {
            var accesses = GetAccesses(calls, group);

            if (accesses.Count == 0 || GetIterator(store, group, accesses[^1].Position) != iterator)
            {
                continue;
            }

            foreach (var position in new[] { accesses[^1].Position, accesses[0].Position })
            {
                AddParts(store, Read(store, group, position), position, parts);
            }
        }

        var blocks = new List<(int Block, string? Category)>();

        for (var block = 0; block < store.Blocks.Count; block++)
        {
            var info = store.Blocks[block];

            if (info.Iterator != iterator)
            {
                continue;
            }

            var category = parts.GetValueOrDefault(block)
                           ?? (IsBucketPage(calls.GetNode(info.CallNodeIndex))
                               ? OtherBucketPagesName
                               : GetMemoryCategory(addressMap.GetLabel(info.Address, info.FromPosition, info.ToPosition)));

            blocks.Add((block, category));
        }

        return TraceMemoryUsage.Build(store, blocks);
    }

    public static List<ulong> GetGroups(TraceCallStore calls)
    {
        var groups = new HashSet<ulong>();

        foreach (var (list, _) in GetGroupCalls(calls))
        {
            for (var index = 0; index < list.Count; index++)
            {
                groups.Add(list[index].GetSlot(GroupArgument));
            }
        }

        return [.. groups];
    }

    public static ulong? GetGroup(MemoryHistoryStore store, TraceCallStore calls, HashSet<ulong> iterators)
    {
        foreach (var group in GetGroups(calls))
        {
            var accesses = GetAccesses(calls, group);

            if (accesses.Count == 0)
            {
                continue;
            }

            var memory = new MemoryHistoryReader(store, accesses[0].Position);

            if (memory.TryRead(group + TraceHashTableLayout.IteratorOffset, out ulong iterator) && iterators.Contains(iterator))
            {
                return group;
            }
        }

        return null;
    }

    public static (ulong Group, (int Partition, int Bucket)? Location)? GetCallTarget(MemoryHistoryStore store,
                                                                                     TraceCallStore calls,
                                                                                     CallTreeNode? node,
                                                                                     int callIndex)
    {
        if (node?.Frame is not { Function: { } function } frame
            || function.ClassName is not (TraceHashTableLayout.GroupClass or TraceHashTableLayout.PartitionClass)
            || callIndex < 0
            || calls.GetCallList(frame.Address, frame.InstanceAddress) is not { } list
            || callIndex >= list.Count)
        {
            return null;
        }

        var call = list[callIndex];

        var position = list.GetFromPosition(callIndex);

        var group = call.GetSlot(GroupArgument);

        if (function.ClassName == TraceHashTableLayout.PartitionClass
            && !new MemoryHistoryReader(store, position).TryRead(group + TraceHashTableLayout.GroupOffset, out group))
        {
            return null;
        }

        var location = function.MethodName is InsertMethod or LookupMethod && Read(store, group, position) is { } table
            ? table.GetLocation((uint)call.GetSlot(HashArgument))
            : null;

        return (group, location);
    }

    public static List<TraceHashAccess> GetAccesses(TraceCallStore calls, ulong group)
        => GetAccesses(calls, new HashSet<ulong> { group })[group];

    public static Dictionary<ulong, List<TraceHashAccess>> GetAccesses(TraceCallStore calls, IReadOnlySet<ulong> groups)
    {
        var accesses = groups.ToDictionary(g => g, _ => new List<TraceHashAccess>());

        foreach (var (list, isInsert) in GetGroupCalls(calls))
        {
            for (var index = 0; index < list.Count; index++)
            {
                var call = list[index];

                if (accesses.TryGetValue(call.GetSlot(GroupArgument), out var found))
                {
                    var hash = (uint)call.GetSlot(HashArgument);

                    found.Add(new TraceHashAccess(list.GetFromPosition(index), hash, isInsert, call.ReturnedValue != 0));
                }
            }
        }

        foreach (var list in accesses.Values)
        {
            list.Sort((left, right) => left.Position.CompareTo(right.Position));
        }

        return accesses;
    }

    public static ulong? GetIterator(MemoryHistoryStore store, ulong group, TracePosition position)
        => new MemoryHistoryReader(store, position).TryRead(group + TraceHashTableLayout.IteratorOffset, out ulong iterator)
            ? iterator
            : null;

    public static ulong GetCurrentGroup(MemoryHistoryStore store, ulong iterator, TracePosition position)
        => new MemoryHistoryReader(store, position).TryRead(iterator + TraceHashTableLayout.CurrentGroupOffset, out ulong group)
            ? group
            : 0;

    public static List<(int Slot, ulong Group, bool IsWorktable)> GetIteratorGroups(MemoryHistoryStore store,
                                                                                    AddressMap map,
                                                                                    ulong iterator,
                                                                                    TracePosition position)
    {
        var memory = new MemoryHistoryReader(store, position);

        var groups = new List<(int Slot, ulong Group, bool IsWorktable)>();

        for (var index = 0; index < TraceHashTableLayout.IteratorGroupCount; index++)
        {
            var slot = iterator + TraceHashTableLayout.IteratorGroupsOffset + (ulong)(index * sizeof(ulong));

            if (!memory.TryRead(slot, out ulong group)
                || group == 0
                || groups.Exists(g => g.Group == group)
                || store.GetBlock(group, position) is not (>= 0 and var block))
            {
                continue;
            }

            var info = store.Blocks[block];

            var label = map.GetLabel(info.Address, info.FromPosition, info.ToPosition);

            if (label is TraceHashTableLayout.GroupClass or TraceHashTableLayout.WorktableGroupClass)
            {
                groups.Add((index, group, label == TraceHashTableLayout.WorktableGroupClass));
            }
        }

        return groups;
    }

    private static bool IsBucketPage(CallTreeNode? node)
    {
        for (var depth = 0; node is not null && depth < MaximumAllocatorDepth; depth++, node = node.Parent)
        {
            if (node.Frame?.Function?.MethodName == BucketPageAllocator)
            {
                return true;
            }
        }

        return false;
    }

    private static void AddParts(MemoryHistoryStore store, TraceHashTable? table, TracePosition position, Dictionary<int, string> parts)
    {
        foreach (var part in table?.GetParts() ?? [])
        {
            if (part.Address != 0 && store.GetBlock(part.Address, position) is >= 0 and var block)
            {
                parts.TryAdd(block, GetPartCategory(part.Type));
            }
        }
    }

    private static string GetPartCategory(TraceHashPartType type) => type switch
    {
        TraceHashPartType.Group => "Partition Groups",
        TraceHashPartType.Partition => "Partitions",
        TraceHashPartType.Directory => "Directories",
        TraceHashPartType.PointerPage => "Pointer Pages",
        TraceHashPartType.BucketPage => "Item Pages",
        TraceHashPartType.OverflowPage => "Overflow Pages",
        TraceHashPartType.WorkfilePage => "Workfile Pages",
        TraceHashPartType.WorkfileRowset => "Workfile Rowsets",
        _ => "Bitmaps"
    };

    private static string? GetMemoryCategory(string? label) => label switch
    {
        WorkfilePageReader.ClassName => "Workfile Pages",
        PartitionClass => "Partitions",
        GroupClass => "Partition Groups",
        WorktableGroupClass => "Worktable Groups",
        IteratorClass => "Hash Iterator",
        _ => null
    };

    private static (ulong Array, List<TraceHashColumn> Columns) ReadColumns(MemoryHistoryStore store,
                                                                           MemoryHistoryReader memory,
                                                                           TracePosition position,
                                                                           ulong group)
    {
        if (!memory.TryRead(group + TraceHashTableLayout.ColumnsOffset, out ulong values)
            || store.GetBlock(values, position) is not (>= 0 and var block))
        {
            return (0, []);
        }

        memory.TryRead(group + TraceHashTableLayout.ScanColumnsOffset, out ulong scanValues);

        var count = store.Blocks[block].Size / sizeof(ulong);

        var columns = new List<TraceHashColumn>(count);

        for (var index = 0; index < count; index++)
        {
            var offset = (ulong)(index * sizeof(ulong));

            if (!memory.TryRead(values + offset, out ulong value)
                || !memory.TryRead(value + TraceHashTableLayout.ValueAccessorOffset, out ulong accessor)
                || !memory.TryRead(accessor, out ulong bindings)
                || AccessorBindingLayout.ReadHeader(memory, bindings) is not { ColumnCount: > 0 } header
                || AccessorBindingLayout.ReadColumns(memory, header.Columns, 1) is not [var binding, ..])
            {
                return (values, []);
            }

            var scanValue = scanValues != 0 && memory.TryRead(scanValues + offset, out ulong scan) ? scan : 0;

            columns.Add(new TraceHashColumn(binding, value, scanValue, accessor, bindings));
        }

        return (values, columns);
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

    private static TraceHashPartition ReadPartition(MemoryHistoryStore store,
                                                    MemoryHistoryReader memory,
                                                    TracePosition position,
                                                    int index,
                                                    ulong instance)
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

                if (!memory.TryRead(address + TraceHashTableLayout.HeadOffset, out ulong head) || head == 0)
                {
                    continue;
                }

                buckets.Add(new TraceHashBucket((int)(first + offset), address, ReadItems(memory, head)));
            }
        }

        memory.TryRead(instance + TraceHashTableLayout.WorkfileOffset, out ulong workfile);

        memory.TryRead(instance + TraceHashTableLayout.BitmapOffset, out ulong bitmap);

        return new TraceHashPartition(index, instance, (int)rows, mask, directory, (int)directoryCount, pages, buckets)
        {
            Workfile = workfile,
            Bitmap = bitmap,
            BitmapPages = ReadBitmapPages(store, memory, position, bitmap)
        };
    }

    private static List<ulong> ReadBitmapPages(MemoryHistoryStore store, MemoryHistoryReader memory, TracePosition position, ulong bitmap)
    {
        var pages = new List<ulong>();

        if (bitmap == 0 || store.GetBlock(bitmap, position) is not (>= 0 and var block))
        {
            return pages;
        }

        var end = store.Blocks[block].Address + (ulong)store.Blocks[block].Size;

        for (var slot = bitmap; slot + sizeof(ulong) <= end; slot += sizeof(ulong))
        {
            if (memory.TryRead(slot, out ulong page) && page != 0 && store.GetBlock(page, position) >= 0)
            {
                pages.Add(page);
            }
        }

        return pages;
    }

    private static List<TraceHashItem> ReadItems(MemoryHistoryReader memory, ulong head)
    {
        var items = new List<TraceHashItem>();

        for (var item = head; item != 0 && items.Count < MaximumItems;)
        {
            if (!memory.TryRead(item, out byte count))
            {
                break;
            }

            var entries = new List<TraceHashEntry>();

            for (var entry = 0; entry < Math.Min((int)count, TraceHashTableLayout.EntriesPerItem); entry++)
            {
                memory.TryRead(item + (ulong)TraceHashTableLayout.GetKeyOffset(entry), out uint key);

                memory.TryRead(item + (ulong)TraceHashTableLayout.GetRowOffset(entry), out ulong row);

                entries.Add(new TraceHashEntry(key, row));
            }

            items.Add(new TraceHashItem(item, entries));

            if (!memory.TryRead(item + TraceHashTableLayout.NextOffset, out item))
            {
                break;
            }
        }

        return items;
    }
}
