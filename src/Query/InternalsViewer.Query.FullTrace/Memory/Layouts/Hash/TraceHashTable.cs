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

    public bool Contains(ulong address, int length)
    {
        var end = address + (ulong)length;

        if (IsWithin(Group, address, end))
        {
            return true;
        }

        foreach (var partition in Partitions)
        {
            if (IsWithin(partition.Address, address, end) || IsWithin(partition.Directory, address, end))
            {
                return true;
            }

            foreach (var page in partition.Pages)
            {
                if (IsWithin(page.PointerPage, address, end) || IsWithin(page.BucketPage, address, end))
                {
                    return true;
                }
            }

            foreach (var bucket in partition.Buckets)
            {
                if (IsWithin(bucket.Row, address, end))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsWithin(ulong value, ulong start, ulong end) => value != 0 && value >= start && value < end;
}
