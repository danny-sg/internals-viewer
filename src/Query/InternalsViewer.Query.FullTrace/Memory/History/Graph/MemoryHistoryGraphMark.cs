namespace InternalsViewer.Query.FullTrace.Memory.History.Graph;

public readonly record struct MemoryHistoryGraphMark(TracePosition Position, int ReplayNodeIndex, bool IsReference);
