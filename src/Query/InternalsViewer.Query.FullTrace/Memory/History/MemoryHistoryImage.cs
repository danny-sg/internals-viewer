namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed record MemoryHistoryImage(byte[] Data, bool[] Known, bool[] Changed);
