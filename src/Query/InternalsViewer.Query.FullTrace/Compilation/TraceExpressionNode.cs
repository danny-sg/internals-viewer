namespace InternalsViewer.Query.FullTrace.Compilation;

public sealed record TraceExpressionNode(ulong Address, ulong Operator, string? ClassName, List<TraceExpressionNode> Children);
