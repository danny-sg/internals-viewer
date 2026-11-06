using InternalsViewer.Query.FullTrace.Calls.Tree;

namespace InternalsViewer.Mcp.Models.Context.Queries;

public sealed record McpIndicatorTarget(CallTreeNode Node, int CallIndex, string? Label);
