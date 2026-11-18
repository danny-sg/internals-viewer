namespace InternalsViewer.Query.FullTrace.Compilation;

public sealed record TraceExpressionTree(string Name, TracePosition Position, TraceExpressionNode Root);
