using System;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;

namespace InternalsViewer.UI.App.ViewModels.Query;

public partial class QueryViewModel : ITraceTimelineCommands
{
    private const int ControllerAttempts = 10;

    private const int ControllerDelayMilliseconds = 100;

    private const string DefaultTrackColour = "#0067C0";

    public async Task SetTimelineOptionAsync(string key, bool enabled)
        => (await RequireTimelineAsync()).SetOption(key, enabled);

    public Task AddTrackAsync(McpTrackRequest request)
    {
        if (TickTrackStore.Definitions.Any(d => string.Equals(d.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A track named '{request.Name}' already exists.");
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

        return Task.CompletedTask;
    }

    public async Task SetTrackVisibleAsync(string name, bool isVisible)
        => (await RequireTimelineAsync()).SetTrackVisible(name, isVisible);

    public Task<int> RemoveTracksAsync(string? name)
    {
        if (name is null)
        {
            var count = TickTrackStore.Definitions.Count;

            TickTrackStore.Clear();

            return Task.FromResult(count);
        }

        var definition = TickTrackStore.Definitions.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase))
                         ?? throw new InvalidOperationException($"No custom track '{name}'.");

        TickTrackStore.Remove(definition);

        return Task.FromResult(1);
    }

    public async Task StepAsync(McpStepMode mode, bool forward, int count)
        => (await RequireTimelineAsync()).Step(mode, forward, count);

    public async Task SearchAsync(string? text) => (await RequireTimelineAsync()).Search(text);

    public async Task StepSearchAsync(bool forward, int count)
        => (await RequireTimelineAsync()).StepSearch(forward, count);

    public async Task SelectCallAsync(CallStackNode node, int callIndex, bool zoom)
        => (await RequireTimelineAsync()).SelectCall(node, callIndex, zoom);

    public async Task ZoomAsync(bool toSelection) => (await RequireTimelineAsync()).Zoom(toSelection);

    public async Task PinPositionsAsync(TracePoint[] points) => (await RequireTimelineAsync()).PinPositions(points);

    public async Task<Guid> ShowIndicatorAsync(McpIndicatorRequest request) => (await RequireTimelineAsync()).ShowIndicator(request);

    public async Task<McpIndicatorResult?> WaitForIndicatorAsync(Guid id, TimeSpan timeout)
        => await (await RequireTimelineAsync()).WaitForIndicatorAsync(id, timeout);

    public async Task<bool> ClearIndicatorAsync() => TraceTimeline is not null && (await RequireTimelineAsync()).ClearIndicator();

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

        throw new InvalidOperationException("The Trace Timeline did not open.");
    }
}
