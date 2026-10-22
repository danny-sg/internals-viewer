namespace InternalsViewer.Query.FullTrace;

internal sealed record WatchResult(IReadOnlyList<WatchBatch> Batches, int WatchedRanges)
{
    public static WatchResult Empty { get; } = new([], 0);

    public int HitCount => Batches.Sum(b => b.Hits.Length);
}
