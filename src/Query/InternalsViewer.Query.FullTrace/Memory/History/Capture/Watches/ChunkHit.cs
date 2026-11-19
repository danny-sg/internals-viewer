namespace InternalsViewer.Query.FullTrace.Memory.History.Capture.Watches;

internal readonly record struct ChunkHit(TracePosition Position, ulong Address, int DataOffset, uint Thread, int Size);
