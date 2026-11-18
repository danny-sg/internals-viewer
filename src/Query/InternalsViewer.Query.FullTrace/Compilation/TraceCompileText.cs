namespace InternalsViewer.Query.FullTrace.Compilation;

public sealed record TraceCompileText(int Block, ulong Address, int Size, string Text, TracePosition Position);
