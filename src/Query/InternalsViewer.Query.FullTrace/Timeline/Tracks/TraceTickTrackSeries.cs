using InternalsViewer.Query.FullTrace.Timeline.Ticks;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

public sealed record TraceTickTrackSeries(TraceTickSeries Series, byte[] Values)
{
    internal Dictionary<TraceTrackRule, TraceWatch> Watches { get; init; } = [];

    public TraceWatch GetWatch(TraceTrackRule rule) => Watches.GetValueOrDefault(rule) ?? TraceWatch.Empty;
}
