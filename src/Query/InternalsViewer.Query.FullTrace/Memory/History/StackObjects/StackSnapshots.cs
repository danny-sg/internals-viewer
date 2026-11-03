namespace InternalsViewer.Query.FullTrace.Memory.History.StackObjects;

internal sealed record StackSnapshots(StackObject[] Objects, DataChunk<StackSnapshotRun>[] Chunks, long SnapshotCount)
{
    public static StackSnapshots Empty { get; } = new([], [], 0);

    public int RunCount => Chunks.Sum(c => c.Hits.Length);
}
