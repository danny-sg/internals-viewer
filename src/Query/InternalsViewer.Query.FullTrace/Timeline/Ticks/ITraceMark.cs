using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline.Ticks;

public interface ITraceMark
{
    TraceSpan Of(TraceTimelineAxis axis);
}
