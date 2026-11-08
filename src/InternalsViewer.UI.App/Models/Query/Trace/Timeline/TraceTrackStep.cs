using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed record TraceTrackStep(TraceTickTrack Track, TraceTick Tick, TraceTickDefinition Rule, bool IsReturn)
{
    public TracePoint Point => IsReturn ? Tick.Span.To : Tick.Span.From;
}
