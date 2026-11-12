using InternalsViewer.Query.FullTrace.Memory.BufferPool;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

public readonly record struct PlanOutputRecord(TraceBufferReference Location, int ColumnOffset);
