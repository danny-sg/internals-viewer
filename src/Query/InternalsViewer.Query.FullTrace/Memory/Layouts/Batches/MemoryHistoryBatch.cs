
using InternalsViewer.Query.FullTrace.Memory.History;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Batches;

public sealed record MemoryHistoryBatch(int Block, BatchState State, int VectorId, MemoryHistoryReader Reader)
{
    public bool HasChanged(TracePosition position) => Reader.HasChanged(position);
}
