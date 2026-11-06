using InternalsViewer.Query.FullTrace.Calls.Tree;

namespace InternalsViewer.Query.FullTrace.Timeline;

public readonly record struct TraceSpanLocation(int ThreadIndex,
                                                TraceTimelineThread Thread,
                                                int Depth,
                                                TraceTimelineRow Row,
                                                int Index,
                                                CallTreeNode Node)
{
    public int CallIndex => Row.GetCallIndex(Index);

    public TracePoint From => Row.GetFrom(Index);

    public TracePoint To => Row.GetTo(Index);

    public TraceSpan Span => Row.GetSpan(Index);
}
