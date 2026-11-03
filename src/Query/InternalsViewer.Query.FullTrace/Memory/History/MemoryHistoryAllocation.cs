namespace InternalsViewer.Query.FullTrace.Memory.History;

internal readonly record struct MemoryHistoryAllocation(MemoryBlock Block, bool IsClerkPage, bool IsBuffer = false);
