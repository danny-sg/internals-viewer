using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace;

public readonly record struct TracePoint(TracePosition Position, ulong Instructions)
{
    public int CompareTo(TracePoint other, TraceTimelineAxis axis)
        => axis == TraceTimelineAxis.Position ? Position.CompareTo(other.Position) : Instructions.CompareTo(other.Instructions);
}
