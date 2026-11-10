namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

public sealed record TraceHashTable(ulong Group,
                                    ulong Iterator,
                                    uint PartitionMask,
                                    int PartitionShift,
                                    IReadOnlyList<TraceHashPartition> Partitions)
{
    public (int Partition, int Bucket)? GetLocation(uint hash)
    {
        var partition = PartitionShift is >= 0 and < 32 ? (int)((hash & PartitionMask) >> PartitionShift) : 0;

        return partition < Partitions.Count ? (partition, (int)(hash & Partitions[partition].BucketMask)) : null;
    }
}
