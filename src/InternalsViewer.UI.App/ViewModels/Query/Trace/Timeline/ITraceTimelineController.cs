using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Query.CallStack;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;

public interface ITraceTimelineController
{
    McpTraceTimeline GetState();

    string SetOption(string key, bool enabled);

    string SetTrackVisible(string name, bool isVisible);

    string Step(McpStepMode mode, bool forward, int count);

    string Search(string? text);

    string StepSearch(bool forward, int count);

    string SelectCall(CallStackNode node, int callIndex, bool zoom);

    string Zoom(bool toSelection);
}
