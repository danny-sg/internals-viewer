using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.Mcp.Documents;

public sealed record McpTraceRange(string Axis, double Start, double End, TracePosition? From, TracePosition? To);
