namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

public readonly record struct TraceHashAccess(TracePosition Position, uint Hash, bool IsInsert, bool IsMatch);
