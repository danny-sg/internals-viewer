using InternalsViewer.Query.FullTrace.Timeline.Ticks;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

internal static class TraceOperatorOpenReader
{
    public static TraceTickSeries Build(TraceTimeline timeline, TraceOperatorLifetimeList lifetimes)
    {
        var sources = new List<TraceTickSource>(lifetimes.Count);

        foreach (var lifetime in lifetimes)
        {
            if (timeline.FindSpan(lifetime.Thread, lifetime.Calls[0]) is not { } location)
            {
                continue;
            }

            sources.Add(new TraceTickSource(location.Thread, location.Row.GetStoreIndex(location.Index), 0, lifetime.Start, lifetime.End)
            {
                Operator = lifetime.Operator
            });
        }

        return TraceTickSeries.Create(timeline, sources);
    }
}
