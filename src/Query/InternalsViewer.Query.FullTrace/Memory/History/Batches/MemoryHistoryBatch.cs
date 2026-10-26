namespace InternalsViewer.Query.FullTrace.Memory.History.Batches;

public sealed record MemoryHistoryBatch(int Block, BatchState State, int VectorId, MemoryHistoryReader Reader)
{
    public bool HasChanged(TracePosition position) => Reader.HasChanged(position);
}
