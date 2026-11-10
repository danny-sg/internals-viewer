namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

public sealed record TraceHashPartition(int Index, ulong Address, int RowCount, uint BucketMask, IReadOnlyList<TraceHashBucket> Buckets)
{
    public int BucketCount => (int)BucketMask + 1;
}
