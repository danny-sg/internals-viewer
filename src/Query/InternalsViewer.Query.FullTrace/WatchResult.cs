namespace InternalsViewer.Query.FullTrace;

internal sealed record WatchResult(IReadOnlyList<WatchBatch> Batches, int WatchedRanges, long ReceivedHitCount = 0)
{
    public static WatchResult Empty { get; } = new([], 0);

    public int HitCount => Batches.Sum(b => b.Hits.Length);
}
