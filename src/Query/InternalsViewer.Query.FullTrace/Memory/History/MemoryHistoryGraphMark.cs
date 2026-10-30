namespace InternalsViewer.Query.FullTrace.Memory.History;

public readonly record struct MemoryHistoryGraphMark(TracePosition Position, int CallNodeIndex, bool IsReference);
