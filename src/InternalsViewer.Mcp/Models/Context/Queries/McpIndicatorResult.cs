using InternalsViewer.Mcp.Models.Context.Queries.Enums;

namespace InternalsViewer.Mcp.Models.Context.Queries;

public sealed record McpIndicatorResult(McpIndicatorClose Reason, int Step, int Steps);
