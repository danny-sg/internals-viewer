using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public readonly record struct FullTraceResolvedSpan(int ThreadIndex,
                                                    FullTraceTimelineThread Thread,
                                                    int Depth,
                                                    FullTraceTimelineRow Row,
                                                    int Index,
                                                    CallStackNode Node)
{
    public int Call => Row.CallAt(Index);

    public double StartOf(FullTraceTimelineAxis axis) => Row.Starts(axis)[Index];

    public double EndOf(FullTraceTimelineAxis axis) => Row.Ends(axis)[Index];

    public FullTraceTimelineSpan Span(FullTraceTimelineAxis axis) => Row.Span(axis, Index);
}
