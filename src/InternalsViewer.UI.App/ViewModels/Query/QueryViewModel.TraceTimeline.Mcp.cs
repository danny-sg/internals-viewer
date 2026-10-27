using System;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;

namespace InternalsViewer.UI.App.ViewModels.Query;

public partial class QueryViewModel : ITraceTimelineCommands
{
    private const int ControllerAttempts = 10;

    private const int ControllerDelayMilliseconds = 100;

    private const string DefaultTrackColour = "#0067C0";

    public async Task<string> SetTimelineOptionAsync(string key, bool enabled)
        => (await RequireTimelineAsync()).SetOption(key, enabled);

    public Task<string> AddTrackAsync(McpTrackRequest request)
    {
        if (TickTrackStore.Definitions.Any(d => string.Equals(d.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A track named '{request.Name}' already exists. Remove it first or choose another name.");
        }

        var node = (CallStack?.Nodes() ?? []).FirstOrDefault(n => IsFunction(n, request.Module, request.Function));

        var definition = new TraceTickDefinition(request.Name,
                                                 request.Module,
                                                 request.Function,
                                                 request.Colour ?? node?.CategoryColour ?? DefaultTrackColour,
                                                 1,
                                                 1,
                                                 request.Conditions);

        AddTickTrack(definition);

        return Task.FromResult($"Added the {request.Name} track for {request.Module}!{request.Function}.");
    }

    public async Task<string> SetTrackVisibleAsync(string name, bool isVisible)
        => (await RequireTimelineAsync()).SetTrackVisible(name, isVisible);

    public Task<string> RemoveTracksAsync(string? name)
    {
        if (name is null)
        {
            var count = TickTrackStore.Definitions.Count;

            TickTrackStore.Clear();

            return Task.FromResult($"Removed {count} custom tracks.");
        }

        var definition = TickTrackStore.Definitions.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase))
                         ?? throw new InvalidOperationException(
                             $"No custom track '{name}'. Built-in tracks can be hidden but not removed.");

        TickTrackStore.Remove(definition);

        return Task.FromResult($"Removed the {definition.Name} track.");
    }

    public async Task<string> StepAsync(McpStepMode mode, bool forward, int count)
        => (await RequireTimelineAsync()).Step(mode, forward, count);

    public async Task<string> SearchAsync(string? text) => (await RequireTimelineAsync()).Search(text);

    public async Task<string> StepSearchAsync(bool forward, int count)
        => (await RequireTimelineAsync()).StepSearch(forward, count);

    public async Task<string> SelectCallAsync(CallStackNode node, int callIndex, bool zoom)
        => (await RequireTimelineAsync()).SelectCall(node, callIndex, zoom);

    public async Task<string> ZoomAsync(bool toSelection) => (await RequireTimelineAsync()).Zoom(toSelection);

    private static bool IsFunction(CallStackNode node, string module, string function)
        => node.Frame is { } frame && string.Equals(frame.Module, module, StringComparison.OrdinalIgnoreCase) && node.Symbol == function;

    private async Task<ITraceTimelineController> RequireTimelineAsync()
    {
        if (TraceTimeline is null)
        {
            throw new InvalidOperationException("The Trace Timeline needs a Full Trace run.");
        }

        Layout.ShowTraceTimeline();

        for (var attempt = 0; attempt < ControllerAttempts; attempt++)
        {
            if (TraceTimelineTab.Controller is { } controller)
            {
                return controller;
            }

            await Task.Delay(ControllerDelayMilliseconds);
        }

        throw new InvalidOperationException("The Trace Timeline did not open. Try again.");
    }
}
