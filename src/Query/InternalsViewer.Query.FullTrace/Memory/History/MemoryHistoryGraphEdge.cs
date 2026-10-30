namespace InternalsViewer.Query.FullTrace.Memory.History;

public readonly record struct MemoryHistoryGraphEdge(int From, int To, int Offset, int? TargetOffset, int ChangeCount);
