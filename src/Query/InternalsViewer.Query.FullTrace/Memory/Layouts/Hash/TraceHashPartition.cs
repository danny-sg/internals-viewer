namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

public sealed record TraceHashPartition(int Index,
                                        ulong Address,
                                        int RowCount,
                                        uint BucketMask,
                                        ulong Directory,
                                        int DirectoryCount,
                                        IReadOnlyList<TraceHashPage> Pages,
                                        IReadOnlyList<TraceHashBucket> Buckets)
{
    public ulong Workfile { get; init; }

    public ulong Bitmap { get; init; }

    public List<ulong> BitmapPages { get; init; } = [];

    public int BucketCount => (int)BucketMask + 1;
}
