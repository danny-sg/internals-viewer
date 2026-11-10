using System.Buffers.Binary;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.History;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

public static class TraceHashTableReader
{
    private const string GroupClass = "CHashWorkfilePartitionGroup";

    private const string InsertMethod = "Insert";

    private const string LookupMethod = "Lookup";

    private const int GroupArgument = 0;

    private const int HashArgument = 2;

    private const int IteratorOffset = 0x10;

    private const int PartitionListOffset = 0xF0;

    private const int PartitionLinkOffset = 0x98;

    private const int RowCountOffset = 0x20;

    private const int DirectoryOffset = 0x48;

    private const int DirectoryCountOffset = 0x50;

    private const int BucketMaskOffset = 0x54;

    private const int PartitionMaskOffset = 0x188;

    private const int PartitionShiftOffset = 0x18C;

    private const int DirectoryShift = 16;

    private const int PageShift = 6;

    private const uint PageIndexMask = 0x3FF;

    private const int BucketsPerPage = 64;

    private const int BucketSize = 0x80;

    private const int KeyOffset = 0x20;

    private const int RowOffset = 0x50;

    private const int MaximumPartitions = 256;

    public static TraceHashTable? Read(MemoryHistoryStore store, ulong group, TracePosition position)
    {
        var memory = new MemoryReader(store, position);

        if (memory.ReadUInt64(group + IteratorOffset) is not { } iterator)
        {
            return null;
        }

        var partitions = new List<TraceHashPartition>();

        var head = group + PartitionListOffset;

        var link = memory.ReadUInt64(head + sizeof(ulong));

        while (link is { } current and not 0 && current != head && partitions.Count < MaximumPartitions)
        {
            partitions.Add(ReadPartition(memory, partitions.Count, current - PartitionLinkOffset));

            link = memory.ReadUInt64(current + sizeof(ulong));
        }

        return new TraceHashTable(group,
                                  iterator,
                                  memory.ReadUInt32(iterator + PartitionMaskOffset) ?? 0,
                                  (int)(memory.ReadUInt32(iterator + PartitionShiftOffset) ?? 0),
                                  partitions);
    }

    public static List<TraceHashAccess> GetAccesses(TraceCallStore calls, ulong group)
    {
        var accesses = new List<TraceHashAccess>();

        foreach (var list in calls.GetCallLists())
        {
            if (calls.GetFirstNode(list)?.Frame?.Function is not { ClassName: GroupClass } function
                || function.MethodName is not (InsertMethod or LookupMethod))
            {
                continue;
            }

            var isInsert = function.MethodName == InsertMethod;

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

    private static TraceHashPartition ReadPartition(MemoryReader memory, int index, ulong instance)
    {
        var mask = memory.ReadUInt32(instance + BucketMaskOffset) ?? 0;

        var buckets = new List<TraceHashBucket>();

        if (memory.ReadUInt64(instance + DirectoryOffset) is { } directory and not 0
            && memory.ReadUInt32(instance + DirectoryCountOffset) is { } directoryCount and > 0)
        {
            for (var first = 0L; first <= mask; first += BucketsPerPage)
            {
                var entry = (ulong)((first >> DirectoryShift) & (directoryCount - 1));

                var level = memory.ReadUInt64(directory + entry * sizeof(ulong)) ?? 0;

                var page = level == 0 ? 0 : memory.ReadUInt64(level + ((ulong)(first >> PageShift) & PageIndexMask) * sizeof(ulong)) ?? 0;

                if (page == 0)
                {
                    continue;
                }

                var count = Math.Min(BucketsPerPage, mask + 1 - first);

                for (var offset = 0; offset < count; offset++)
                {
                    var address = page + (ulong)(offset * BucketSize);

                    if (memory.ReadByte(address) is { } rows and > 0)
                    {
                        buckets.Add(new TraceHashBucket((int)(first + offset),
                                                        address,
                                                        rows,
                                                        memory.ReadUInt64(address + KeyOffset) ?? 0,
                                                        memory.ReadUInt64(address + RowOffset) ?? 0));
                    }
                }
            }
        }

        return new TraceHashPartition(index, instance, (int)(memory.ReadUInt32(instance + RowCountOffset) ?? 0), mask, buckets);
    }

    private sealed class MemoryReader(MemoryHistoryStore store, TracePosition position)
    {
        private Dictionary<int, MemoryHistoryImage?> Images { get; } = [];

        public byte? ReadByte(ulong address) => Read(address, sizeof(byte)) is { Length: sizeof(byte) } bytes ? bytes[0] : null;

        public uint? ReadUInt32(ulong address)
            => Read(address, sizeof(uint)) is { Length: sizeof(uint) } bytes ? BinaryPrimitives.ReadUInt32LittleEndian(bytes) : null;

        public ulong? ReadUInt64(ulong address)
            => Read(address, sizeof(ulong)) is { Length: sizeof(ulong) } bytes ? BinaryPrimitives.ReadUInt64LittleEndian(bytes) : null;

        private ReadOnlySpan<byte> Read(ulong address, int length)
        {
            var block = store.GetBlock(address, position);

            if (block < 0)
            {
                return [];
            }

            if (!Images.TryGetValue(block, out var image))
            {
                var count = store.GetWriteCount(block, position);

                image = count > 0 ? store.GetImage(block, count - 1) : null;

                Images[block] = image;
            }

            var offset = (long)(address - store.Blocks[block].Address);

            if (image is null || offset + length > image.Data.Length || image.Known.AsSpan((int)offset, length).Contains(false))
            {
                return [];
            }

            return image.Data.AsSpan((int)offset, length);
        }
    }
}
