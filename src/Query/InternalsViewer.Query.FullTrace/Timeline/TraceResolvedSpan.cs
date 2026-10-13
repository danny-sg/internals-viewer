using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public readonly record struct TraceResolvedSpan(int ThreadIndex,
                                                TraceTimelineThread Thread,
                                                int Depth,
                                                TraceTimelineRow Row,
                                                int Index,
                                                CallStackNode Node)
{
    public int Call => Row.CallAt(Index);

    public double StartOf(TraceTimelineAxis axis) => Row.Starts(axis)[Index];

    public double EndOf(TraceTimelineAxis axis) => Row.Ends(axis)[Index];

    public TraceTimelineSpan Span(TraceTimelineAxis axis) => Row.Span(axis, Index);
}
