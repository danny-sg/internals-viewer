using System;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace;
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

    SKColor GetColour(int callNodeIndex);

    string GetLabel(int callNodeIndex);

    void Invalidate();

    void Post(Action action);

    void ShowMemoryProgress(bool isVisible);

    void MovePlayhead(double position);

    void ClickCall(CallStackNode node, int callIndex);

    void OpenCallTree(CallStackNode node, int callIndex);

    void OnSelectedOperatorChanged(ExecutionOperatorEvent? selected);

    void SelectMemoryWrite(int block, TracePosition? position);

    void ShowMemory(int block, ulong address, TracePosition position);
}
