namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed record MemoryHistorySummary(IReadOnlyList<ulong> Operators, int CallCount, int ChangeCount, int ChangingByteCount);
