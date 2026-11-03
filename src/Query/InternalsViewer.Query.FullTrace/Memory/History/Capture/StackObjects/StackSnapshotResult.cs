namespace InternalsViewer.Query.FullTrace.Memory.History.Capture.StackObjects;

internal sealed record StackSnapshotResult(StackObject[] Objects, DataChunk<StackSnapshotRun>[] Chunks, long SnapshotCount)
{
    public static StackSnapshotResult Empty { get; } = new([], [], 0);

    public int RunCount => Chunks.Sum(c => c.Hits.Length);
}
