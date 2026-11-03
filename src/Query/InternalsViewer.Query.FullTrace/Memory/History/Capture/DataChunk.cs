namespace InternalsViewer.Query.FullTrace.Memory.History.Capture;

internal sealed record DataChunk<THit>(THit[] Hits, byte[] Data);
