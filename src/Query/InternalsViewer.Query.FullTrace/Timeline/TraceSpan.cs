using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public readonly record struct TraceSpan(TracePoint From, TracePoint To, int CallNodeIndex, int CallIndex, TraceSpanFlags Flags)
{
    public ulong Instructions => To.Instructions - From.Instructions;
}
