using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

internal static class TraceSeekReader
{
    private const string Module = "sqlmin";

    private const string PageManagerClass = "IndexPageManager";

    private const string GetPageWithKeyMethod = "GetPageWithKey";

    public static TraceTickSeries Build(TraceTimeline timeline,
                                        TraceCallStore calls,
                                        TraceOperatorLifetimeList lifetimes,
                                        EnclosingOperatorCallIndex enclosing)
    {
        var seeks = new Dictionary<uint, List<Seek>>();

        var fetches = new List<TraceSpanLocation>();

        foreach (var list in calls.GetCallLists())
        {
            if (calls.GetFirstNode(list)?.Frame is not { } frame)
            {
                continue;
            }

            var isSeek = IsSeek(frame);

            if (!isSeek && !IsFetch(frame))
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                if (timeline.FindCall(list.FunctionAddress, list.InstanceAddress, index) is not { } location)
                {
                    continue;
                }

                if (isSeek)
                {
                    GetThreadSeeks(seeks, location.Thread.ThreadId).Add(new Seek(location, []));
                }
                else
                {
                    fetches.Add(location);
                }
            }
        }

        foreach (var threadSeeks in seeks.Values)
        {
            threadSeeks.Sort(static (left, right) => left.Location.From.Position.CompareTo(right.Location.From.Position));
        }

        foreach (var fetch in fetches.OrderBy(f => f.From.Position))
        {
            if (seeks.TryGetValue(fetch.Thread.ThreadId, out var threadSeeks) && GetEnclosing(threadSeeks, fetch) is { } seek)
            {
                seek.Fetches.Add(fetch);
            }
        }

        var sources = new List<TraceTickSource>();

        foreach (var seek in seeks.Values.SelectMany(s => s))
        {
            sources.Add(TraceTickSource.FromCall(seek.Location, (byte)TraceSeekMarkType.Seek));

            for (var index = 0; index < seek.Fetches.Count; index++)
            {
                sources.Add(TraceTickSource.FromCall(seek.Fetches[index], (byte)GetLevel(index, seek.Fetches.Count)));
            }
        }

        return TraceTickSeries.FromSources(timeline, sources, lifetimes, enclosing);
    }

    private static bool IsSeek(CallstackFrame frame)
        => frame is { Resolved: { ClassName: PageManagerClass, MethodName: GetPageWithKeyMethod } }
           && frame.Module.Equals(Module, StringComparison.OrdinalIgnoreCase);

    private static bool IsFetch(CallstackFrame frame)
        => BufferPoolFunctionLookup.IsBufferPool(frame) && frame.Resolved?.MethodName == BufferPoolFunctionLookup.GetMethod;

    private static List<Seek> GetThreadSeeks(Dictionary<uint, List<Seek>> seeks, uint thread)
    {
        if (!seeks.TryGetValue(thread, out var threadSeeks))
        {
            threadSeeks = [];

            seeks[thread] = threadSeeks;
        }

        return threadSeeks;
    }

    private static Seek? GetEnclosing(List<Seek> seeks, TraceSpanLocation fetch)
    {
        var position = fetch.From.Position;

        var index = SortedSearch.FirstAfter(CollectionsMarshal.AsSpan(seeks), position, s => s.Location.From.Position) - 1;

        return index >= 0 && position <= seeks[index].Location.To.Position ? seeks[index] : null;
    }

    private static TraceSeekMarkType GetLevel(int index, int count)
        => index == 0 ? TraceSeekMarkType.Root : index == count - 1 ? TraceSeekMarkType.Leaf : TraceSeekMarkType.Intermediate;

    private sealed record Seek(TraceSpanLocation Location, List<TraceSpanLocation> Fetches);
}
