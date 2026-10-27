using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Mcp.Models.Context.Queries;

/// <summary>
/// The state of a Query tab's Trace Timeline, its menu options, tracks, search and playback
/// </summary>
public sealed record McpTraceTimeline
{
    public string Axis { get; init; } = string.Empty;

    public bool IsPlaying { get; init; }

    public string? SearchText { get; init; }

    public int? SearchMatches { get; init; }

    public CallStackNode? SelectedNode { get; init; }

    public int SelectedCallIndex { get; init; } = -1;

    public IReadOnlyList<McpOption> Options { get; init; } = [];

    public IReadOnlyList<McpTimelineTrack> Tracks { get; init; } = [];
}
