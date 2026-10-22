namespace InternalsViewer.Query.FullTrace.Memory.History.Watches;

internal sealed record WatchResult(IReadOnlyList<WatchChunk> Chunks, int WatchedRanges, long ReceivedHitCount = 0)
{
    public static WatchResult Empty { get; } = new([], 0);

    public int HitCount => Chunks.Sum(c => c.Hits.Length);
}
