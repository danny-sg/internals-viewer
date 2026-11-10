using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

internal static class TracePageFetchTrackBuilder
{
    private const int PfsInterval = 8088;

    public static TraceTickSeries Build(TraceTimeline timeline,
                                        TracePageAccessList accesses,
                                        TraceBufferPool pool,
                                        TraceOperatorLifetimeList lifetimes,
                                        EnclosingOperatorCallIndex enclosing)
    {
        var sources = new List<TraceTickSource>();

        for (var index = 0; index < accesses.Count; index++)
        {
            var access = accesses[index];

            if (access.Offset is not null
                || timeline.GetNode(access.CallNodeIndex) is not { } node
                || timeline.FindCall(node, access.CallIndex) is not { } location)
            {
                continue;
            }

            sources.Add(TraceTickSource.Create(location, (byte)GetPageType(access, pool)));
        }

        return TraceTrackBuilder.Build(timeline, sources, lifetimes, enclosing);
    }

    internal static PageType GetPageType(TracePageAccess access, TraceBufferPool pool)
        => pool.GetLayout(access.Page, access.DatabaseId)?.PageHeader.PageType is { } type and not PageType.None
            ? type
            : IsPfs(access.Page.PageId) ? PageType.Pfs : PageType.None;

    private static bool IsPfs(int pageId) => pageId == 1 || (pageId > 0 && pageId % PfsInterval == 0);
}
