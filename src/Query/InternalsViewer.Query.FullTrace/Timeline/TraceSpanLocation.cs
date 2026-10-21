using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Query.FullTrace.Timeline;

public readonly record struct TraceSpanLocation(int ThreadIndex,
                                                TraceTimelineThread Thread,
                                                int Depth,
                                                TraceTimelineRow Row,
                                                int Index,
                                                CallStackNode Node)
{
    public int Call => Row.CallAt(Index);

    public TracePoint From => Row.GetFrom(Index);

    public TracePoint To => Row.GetTo(Index);

    public TraceSpan Span => Row.GetSpan(Index);
}
