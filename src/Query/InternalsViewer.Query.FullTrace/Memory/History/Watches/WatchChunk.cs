namespace InternalsViewer.Query.FullTrace.Memory.History.Watches;

internal sealed record WatchChunk(ChunkHit[] Hits, byte[] Data);
