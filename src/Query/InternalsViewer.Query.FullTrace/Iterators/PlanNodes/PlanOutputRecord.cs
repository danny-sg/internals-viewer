using InternalsViewer.Query.FullTrace.Memory.BufferPool;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

public readonly record struct PlanOutputRecord(TraceBufferReference Location, int ColumnOffset);
