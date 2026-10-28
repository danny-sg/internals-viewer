using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed record TraceViewRange(TraceTimelineAxis Axis, double Start, double End, TracePosition? From, TracePosition? To);
