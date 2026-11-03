namespace InternalsViewer.Query.FullTrace.Memory.History.Capture.StackObjects;

internal readonly record struct StackSnapshotRun(int Object, TracePosition Position, int Offset, int DataOffset, ushort Length);
