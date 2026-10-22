namespace InternalsViewer.Query.FullTrace;

internal sealed record WatchBatch(WatchedHit[] Hits, byte[] Data);
