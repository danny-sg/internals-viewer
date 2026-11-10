using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

internal static class TraceRowAccessTrackBuilder
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

            if (access.Offset is not null
                && GetLocation(timeline, access) is { } location
                && !recent.IsRecent(location.Thread.ThreadId, access))
            {
                sources.Add(GetSource(location, access));
            }
        }

        return TraceTickSeries.Build(timeline, sources, lifetimes, enclosing);
    }

    public static TraceTickSeries BuildHeaders(TraceTimeline timeline,
                                               TracePageAccessList accesses,
                                               TraceOperatorLifetimeList lifetimes,
                                               EnclosingOperatorCallIndex enclosing)
    {
        var sources = new List<TraceTickSource>();

        foreach (var header in accesses.HeaderAccesses)
        {
            if (GetLocation(timeline, header.Access) is { } location)
            {
                sources.Add(GetSource(location, header.Access));
            }
        }

        return TraceTickSeries.Build(timeline, sources, lifetimes, enclosing);
    }

    private static TraceSpanLocation? GetLocation(TraceTimeline timeline, TracePageAccess access)
        => timeline.GetNode(access.CallNodeIndex) is { } node ? timeline.FindCall(node, access.CallIndex) : null;

    private static TraceTickSource GetSource(TraceSpanLocation location, TracePageAccess access)
    {
        var point = new TracePoint(access.Position, location.Thread.Clock.GetInstructions(access.Position));

        return TraceTickSource.Create(location, 0, point);
    }
}
