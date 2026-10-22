using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace;

internal sealed record WatchBatch(WatchHit[] Hits, byte[] Data);
