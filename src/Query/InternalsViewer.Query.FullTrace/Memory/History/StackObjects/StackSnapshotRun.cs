namespace InternalsViewer.Query.FullTrace.Memory.History.StackObjects;

internal readonly record struct StackSnapshotRun(int Object, TracePosition Position, int Offset, int DataOffset, ushort Length);
