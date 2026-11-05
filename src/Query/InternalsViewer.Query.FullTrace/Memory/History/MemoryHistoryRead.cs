namespace InternalsViewer.Query.FullTrace.Memory.History;

public readonly record struct MemoryHistoryRead(TracePosition Position,
                                                int CallNodeIndex,
                                                int CallIndex,
                                                int Offset,
                                                int Length,
                                                int Count);
