using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed record TraceTrackStep(TraceTickTrack Track, TraceTick Tick, TraceTrackRule Rule, bool IsReturn)
{
    public TracePoint Point => IsReturn ? Tick.Span.To : Tick.Span.From;
}
