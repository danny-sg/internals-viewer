namespace InternalsViewer.Query.FullTrace.Memory.History.Capture.Watches;

internal sealed record WatchResult(WatchHitStore Hits, int WatchedRangeCount, long ReceivedHitCount = 0)
{
    public static WatchResult Empty { get; } = new(new WatchHitStore(), 0);

    public int HitCount => Hits.Count;
}
