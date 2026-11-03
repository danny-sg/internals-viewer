namespace InternalsViewer.Query.FullTrace.Memory.History;

internal sealed record DataChunk<THit>(THit[] Hits, byte[] Data);
