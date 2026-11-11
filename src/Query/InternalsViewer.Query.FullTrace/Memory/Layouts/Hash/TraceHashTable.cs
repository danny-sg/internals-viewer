using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

public sealed record TraceHashTable(ulong Group,
                                    ulong Iterator,
                                    ulong ActivePartition,
                                    uint PartitionMask,
                                    int PartitionShift,
                                    IReadOnlyList<TraceHashPartition> Partitions)
{
    public (int Partition, int Bucket)? GetLocation(uint hash)
    {
        var partition = PartitionShift is >= 0 and < 32 ? (int)((hash & PartitionMask) >> PartitionShift) : 0;

        return partition < Partitions.Count ? (partition, (int)(hash & Partitions[partition].BucketMask)) : null;
    }

    public bool Contains(ulong address, int length) => FindPart(address, length) is not null;

    public TraceHashPart? FindPart(ulong address, int length)
    {
        var end = address + (ulong)length;

        foreach (var part in GetParts())
        {
            if (part.Address != 0 && part.Address >= address && part.Address < end)
            {
                return part;
            }
        }

        return null;
    }

    public IEnumerable<TraceHashPart> GetParts()
    {
        yield return new TraceHashPart(TraceHashPartType.Group, -1, Group);

        foreach (var partition in Partitions)
        {
            var index = partition.Index;

            yield return new TraceHashPart(TraceHashPartType.Partition, index, partition.Address);

            yield return new TraceHashPart(TraceHashPartType.Directory, index, partition.Directory);

            yield return new TraceHashPart(TraceHashPartType.WorkfileRowset, index, partition.Workfile);

            yield return new TraceHashPart(TraceHashPartType.Bitmap, index, partition.Bitmap);

            foreach (var page in partition.BitmapPages)
            {
                yield return new TraceHashPart(TraceHashPartType.BitmapPage, index, page);
            }

            foreach (var page in partition.Pages)
            {
                yield return new TraceHashPart(TraceHashPartType.PointerPage, index, page.PointerPage);

                yield return new TraceHashPart(TraceHashPartType.BucketPage, index, page.BucketPage);
            }

            foreach (var item in partition.Buckets.SelectMany(b => b.Items))
            {
                yield return new TraceHashPart(TraceHashPartType.OverflowPage, index, item.Address);
            }

            foreach (var entry in partition.Buckets.SelectMany(b => b.Entries))
            {
                yield return new TraceHashPart(TraceHashPartType.WorkfilePage, index, entry.Row);
            }
        }
    }
}
