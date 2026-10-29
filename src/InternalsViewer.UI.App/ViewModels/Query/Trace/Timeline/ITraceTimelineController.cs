using System;
using System.Threading.Tasks;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;

public interface ITraceTimelineController
{
    McpTraceTimeline GetState();

    void SetOption(string key, bool enabled);

    void SetTrackVisible(string name, bool isVisible);

    void Step(McpStepMode mode, bool forward, int count);

    void Search(string? text);

    void StepSearch(bool forward, int count);

    void SelectCall(CallStackNode node, int callIndex, bool zoom);

    void Zoom(bool toSelection);

    void PinPositions(TracePoint[] points);

    Guid ShowIndicator(McpIndicatorRequest request);

    Task<McpIndicatorResult?> WaitForIndicatorAsync(Guid id, TimeSpan timeout);

    bool ClearIndicator();
}
