using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.Mcp.Models.Context.Queries;

public sealed record McpIndicatorStep(List<McpIndicatorTarget> Targets, string Comment, bool HasNext, TracePoint? Playhead = null);
