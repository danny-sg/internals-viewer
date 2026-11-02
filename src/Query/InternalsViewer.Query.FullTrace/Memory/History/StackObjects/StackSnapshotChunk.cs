namespace InternalsViewer.Query.FullTrace.Memory.History.StackObjects;

internal sealed record StackSnapshotChunk(StackSnapshotRun[] Runs, byte[] Data);
