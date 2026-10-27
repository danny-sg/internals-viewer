using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Mcp.Interfaces.Commands;

/// <summary>
/// Commands a Query tab's Trace Timeline accepts
/// </summary>
public interface ITraceTimelineCommands
{
    Task<string> SetTimelineOptionAsync(string key, bool enabled);

    Task<string> AddTrackAsync(McpTrackRequest request);

    Task<string> SetTrackVisibleAsync(string name, bool isVisible);

    Task<string> RemoveTracksAsync(string? name);

    Task<string> StepAsync(McpStepMode mode, bool forward, int count);

    Task<string> SearchAsync(string? text);

    Task<string> StepSearchAsync(bool forward, int count);

    Task<string> SelectCallAsync(CallStackNode node, int callIndex, bool zoom);

    Task<string> ZoomAsync(bool toSelection);
}
