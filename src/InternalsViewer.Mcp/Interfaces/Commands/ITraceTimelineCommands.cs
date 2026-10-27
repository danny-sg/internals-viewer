using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Mcp.Interfaces.Commands;

/// <summary>
/// Commands a Query tab's Trace Timeline accepts
/// </summary>
public interface ITraceTimelineCommands
{
    Task SetTimelineOptionAsync(string key, bool enabled);

    Task AddTrackAsync(McpTrackRequest request);

    Task SetTrackVisibleAsync(string name, bool isVisible);

    Task<int> RemoveTracksAsync(string? name);

    Task StepAsync(McpStepMode mode, bool forward, int count);

    Task SearchAsync(string? text);

    Task StepSearchAsync(bool forward, int count);

    Task SelectCallAsync(CallStackNode node, int callIndex, bool zoom);

    Task ZoomAsync(bool toSelection);
}
