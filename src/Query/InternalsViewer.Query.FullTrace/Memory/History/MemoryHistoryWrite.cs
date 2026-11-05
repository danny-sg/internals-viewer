using InternalsViewer.Query.FullTrace.Memory.History.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public readonly record struct MemoryHistoryWrite(int Index,
                                                 TracePosition Position,
                                                 int CallNodeIndex,
                                                 int CallIndex,
                                                 int Offset,
                                                 ushort Length,
                                                 MemoryHistoryAccessType Type);
