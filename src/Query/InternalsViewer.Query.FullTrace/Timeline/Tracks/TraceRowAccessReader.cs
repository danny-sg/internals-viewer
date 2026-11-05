using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

internal static class TraceRowAccessReader
{
    public static TraceTickSeries Build(TraceTimeline timeline,
                                        TracePageAccessList accesses,
                                        TraceOperatorLifetimeList lifetimes,
                                        EnclosingOperatorCallIndex enclosing)
    {
        var sources = new List<TraceTickSource>();

        var recent = new RecentRowFilter();

        for (var index = 0; index < accesses.Count; index++)
        {
            var access = accesses[index];

            if (access.Offset is null
                || timeline.NodeOf(access.CallNodeIndex) is not { } node
                || timeline.FindCall(node, access.CallIndex) is not { } location
                || recent.IsRecent(location.Thread.ThreadId, access))
            {
                continue;
            }

            var at = new TracePoint(access.Position, location.Thread.Clock.GetInstructions(access.Position));

            sources.Add(TraceTickSource.FromPoint(location, 0, at));
        }

        return TraceTickSeries.FromSources(timeline, sources, lifetimes, enclosing);
    }
}
