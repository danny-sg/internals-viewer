using InternalsViewer.Query.FullTrace.Calls;

namespace InternalsViewer.Mcp.Models.Context.Queries;

/// <summary>
/// A custom tick track to add to the Operators pane
/// </summary>
public sealed record McpTrackRequest(
    string Name,
    string Module,
    string Function,
    string? Colour,
    IReadOnlyList<TraceCallCondition> Conditions);
