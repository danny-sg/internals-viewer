using System;
using InternalsViewer.Query.CallStack;
using InternalsViewer.UI.App.Controls.Query.Trace.Timeline.Enums;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed record TraceIndicator(Guid Id, TraceIndicatorStep[] Steps);

public sealed record TraceIndicatorStep(TraceIndicatorTarget[] Targets, string Comment, bool HasNext);

public readonly record struct TraceIndicatorTarget(CallStackNode Node, int CallIndex, string? Label);

public readonly record struct TraceIndicatorResult(TraceIndicatorClose Reason, int Step, int Steps);
