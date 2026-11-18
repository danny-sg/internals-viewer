namespace InternalsViewer.Query.FullTrace.Compilation;

public sealed record TraceCompileObject(string ClassName, ulong Address, int Block, TracePosition Position);
