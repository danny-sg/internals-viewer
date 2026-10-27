using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Query.CallStack;

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
}
