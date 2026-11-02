namespace InternalsViewer.Query.FullTrace.Memory.History.StackObjects;

internal sealed record StackSnapshots(StackObject[] Objects, StackSnapshotChunk[] Chunks, long SnapshotCount)
{
    public static StackSnapshots Empty { get; } = new([], [], 0);

    public int RunCount => Chunks.Sum(c => c.Runs.Length);
}
