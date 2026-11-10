using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

internal static class TraceRowAccessTrackBuilder
{
    private const string SearchClass = "PageRef";

    private const string SearchMethod = "SearchPage";

    public static TraceTickSeries Build(TraceTimeline timeline,
                                        TraceCallStore calls,
                                        TracePageAccessList accesses,
                                        TraceBufferPool pool,
                                        TraceOperatorLifetimeList lifetimes,
                                        EnclosingOperatorCallIndex enclosing)
    {
        var sources = new List<TraceTickSource>();

        var recent = new RecentRowFilter();

        var searches = new Dictionary<int, bool>();

        for (var index = 0; index < accesses.Count; index++)
        {
            var access = accesses[index];

            if (access.Offset is not { } offset
                || GetLocation(timeline, access) is not { } location
                || recent.IsRecent(location.Thread.ThreadId, access))
            {
                continue;
            }

            var isFound = IsInSearch(location.Node, access.CallNodeIndex, searches)
                          && pool.GetLayout(access.Page, access.DatabaseId)?.GetSlot(offset) is { } slot
                          && GetSearchResult(timeline, calls, location, access.Position) == slot;

            sources.Add(GetSource(location, access, isFound ? TraceRowAccessType.Found : TraceRowAccessType.Access));
        }

        return TraceTrackBuilder.Build(timeline, sources, lifetimes, enclosing);
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
                sources.Add(GetSource(location, header.Access, TraceRowAccessType.Access));
            }
        }

        return TraceTrackBuilder.Build(timeline, sources, lifetimes, enclosing);
    }

    private static TraceSpanLocation? GetLocation(TraceTimeline timeline, TracePageAccess access)
        => timeline.GetNode(access.CallNodeIndex) is { } node ? timeline.FindCall(node, access.CallIndex) : null;

    private static TraceTickSource GetSource(TraceSpanLocation location, TracePageAccess access, TraceRowAccessType type)
    {
        var point = new TracePoint(access.Position, location.Thread.Clock.GetInstructions(access.Position));

        return TraceTickSource.Create(location, (byte)type, point);
    }

    private static bool IsInSearch(CallTreeNode node, int callNodeIndex, Dictionary<int, bool> searches)
    {
        if (!searches.TryGetValue(callNodeIndex, out var isInSearch))
        {
            for (var current = node; current is not null && !isInSearch; current = current.Parent)
            {
                isInSearch = IsSearch(current);
            }

            searches[callNodeIndex] = isInSearch;
        }

        return isInSearch;
    }

    private static int? GetSearchResult(TraceTimeline timeline, TraceCallStore calls, TraceSpanLocation location, TracePosition position)
    {
        for (var depth = Math.Min(location.Depth, location.Thread.Rows.Count - 1); depth >= 0; depth--)
        {
            var row = location.Thread.Rows[depth];

            if (row.GetIndex(position) is not (>= 0 and var index)
                || timeline.GetNode(row.GetCallNodeIndex(index)) is not { Frame: { } frame } node
                || !IsSearch(node))
            {
                continue;
            }

            return calls.GetCallList(frame.Address, frame.InstanceAddress) is { } list
                   && row.GetCallIndex(index) is >= 0 and var callIndex
                   && callIndex < list.Count
                   && list[callIndex] is { Returned: true } call
                ? (int)call.ReturnValue
                : null;
        }

        return null;
    }

    private static bool IsSearch(CallTreeNode node) => node.Frame is { Function: { ClassName: SearchClass, MethodName: SearchMethod } };
}
