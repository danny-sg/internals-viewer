namespace InternalsViewer.Query.FullTrace.Memory.History.Capture.Watches;

internal sealed record WatchResult(IReadOnlyList<DataChunk<ChunkHit>> Chunks, int WatchedRangeCount, long ReceivedHitCount = 0)
{
    public static WatchResult Empty { get; } = new([], 0);

    public int HitCount => Chunks.Sum(c => c.Hits.Length);
}
