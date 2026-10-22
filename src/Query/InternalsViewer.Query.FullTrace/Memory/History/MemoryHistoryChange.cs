using InternalsViewer.Query.FullTrace.Memory.History.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public readonly record struct MemoryHistoryChange(int Offset, int DataIndex, ushort Length, MemoryHistoryChangeType Type);
