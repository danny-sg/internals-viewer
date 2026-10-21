using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public readonly record struct TraceSpan(TracePoint From, TracePoint To, int Node, int Call, TraceSpanFlags Flags)
{
    public ulong Instructions => To.Instructions - From.Instructions;
}
