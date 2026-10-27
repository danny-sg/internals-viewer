namespace InternalsViewer.Mcp.Models.Context.Queries;

/// <summary>
/// A tick track on the Operators pane, built in or custom
/// </summary>
public sealed record McpTimelineTrack(
    string Name,
    bool IsCustom,
    bool IsVisible,
    int Ticks,
    string Module,
    string Function,
    IReadOnlyList<string> Conditions);
