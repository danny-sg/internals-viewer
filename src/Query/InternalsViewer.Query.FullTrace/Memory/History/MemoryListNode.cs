namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed record MemoryListNode(int NextOffset, int? ItemOffset, string? ItemClass);
