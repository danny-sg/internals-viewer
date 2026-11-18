namespace InternalsViewer.Query.FullTrace.Memory.History;

public readonly record struct MemoryHistoryRead(TracePosition Position,
                                                int ReplayNodeIndex,
                                                int CallIndex,
                                                int Offset,
                                                int Length,
                                                int Count);
