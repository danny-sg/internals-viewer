namespace InternalsViewer.Query.CallStack.TimeTravel.Timeline;

public readonly record struct TimeTravelResolvedSpan(int ThreadIndex,
                                                     TimeTravelTimelineThread Thread,
                                                     int Depth,
                                                     TimeTravelTimelineRow Row,
                                                     int Index,
                                                     CallStackNode Node)
{
    public int Call => Row.CallAt(Index);

    public double StartOf(TimeTravelTimelineAxis axis) => Row.Starts(axis)[Index];

    public double EndOf(TimeTravelTimelineAxis axis) => Row.Ends(axis)[Index];

    public TimeTravelTimelineSpan Span(TimeTravelTimelineAxis axis) => Row.Span(axis, Index);
}
