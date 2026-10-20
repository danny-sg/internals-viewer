using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Trace;

internal interface ITraceTimelineHost
{
    TraceTimelineAxis Axis { get; }

    double ViewStart { get; }

    double ViewEnd { get; }

    int Width { get; }

    double Scale { get; }

    double? Playhead { get; }

    bool ShowCallStack { get; }

    TraceTimelinePaints Paints { get; }

    SKPathBuilder PathBuilder { get; }

    double GetOrigin(TraceTimelineAxis axis);

    SKColor GetColour(int node);

    string GetLabel(int node);

    void Invalidate();

    void MovePlayhead(double position);

    void ClickCall(CallStackNode node, int call);

    void OpenCallTree(CallStackNode node, int call);

    void OnSelectedOperatorChanged(ExecutionOperatorEvent? selected);
}
