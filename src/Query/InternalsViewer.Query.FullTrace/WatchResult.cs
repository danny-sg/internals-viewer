using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace;

internal sealed record WatchResult(WatchHit[] Hits, byte[] Data, int WatchedRanges)
{
    public static WatchResult Empty { get; } = new([], [], 0);
}
