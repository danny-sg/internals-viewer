using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Mcp.Models.Context.Queries;

public sealed record McpIndicatorTarget(CallStackNode Node, int CallIndex, string? Label);
