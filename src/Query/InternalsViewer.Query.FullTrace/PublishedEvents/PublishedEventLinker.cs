using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Events;

namespace InternalsViewer.Query.FullTrace.PublishedEvents;

internal static class PublishedEventLinker
{
    private const int Lookahead = 64;

    public static Result Link(IReadOnlyList<PublishedEvent> published,
                              IReadOnlyList<EventPublisher> publishers,
                              IReadOnlyList<RawEvent> rawEvents,
                              IEnumerable<EngineEvent> events,
                              TraceTimeline timeline)
    {
        var names = publishers.DistinctBy(p => p.Address).ToDictionary(p => p.Address, p => p.Event);

        if (published.Where(p => p.SessionBuffer != 0).GroupBy(p => p.SessionBuffer).MaxBy(g => g.Count())?.Key is not { } buffer)
        {
            return new Result([], published.Count, 0, 0);
        }

        var written = published.Where(p => p.SessionBuffer == buffer && names.ContainsKey(p.Publisher))
                               .GroupBy(p => p.Thread)
                               .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Position).ToList());

        var raw = rawEvents.Where(e => e.SystemThreadId is not null)
                           .GroupBy(e => e.SystemThreadId!.Value)
                           .ToDictionary(g => g.Key, g => g.ToList());

        var eventsBySequence = events.BySequence();

        var links = new List<TraceEventLink>();

        var paired = 0;

        foreach (var (thread, threadPublished) in written)
        {
            if (!raw.TryGetValue(thread, out var threadRaw))
            {
                continue;
            }

            List<string> publishedNames = [.. threadPublished.Select(p => names[p.Publisher])];

            List<string> rawNames = [.. threadRaw.Select(r => r.Name)];

            List<double> positions = [.. threadPublished.Select(p => timeline.PositionOf(p.Position))];

            var pairs = new List<(int Published, int Raw)>();

            Align(publishedNames, rawNames, 0, publishedNames.Count, 0, rawNames.Count, pairs);

            paired += pairs.Count;

            foreach (var (publishedIndex, rawIndex) in pairs)
            {
                if (threadRaw[rawIndex].SequenceId is not { } sequence || !eventsBySequence.TryGetValue(sequence, out var engineEvent))
                {
                    continue;
                }

                var found = threadPublished[publishedIndex];

                links.Add(new TraceEventLink(engineEvent,
                                             timeline.NodeOf(found.Node),
                                             thread,
                                             positions[publishedIndex],
                                             found.Instructions));
            }
        }

        return new Result([.. links.OrderBy(l => l.Event.TimeUs).ThenBy(l => l.Event.SequenceId)],
                          published.Count,
                          written.Values.Sum(m => m.Count),
                          paired);
    }

    private static void Align(List<string> publishedNames,
                              List<string> rawNames,
                              int published,
                              int publishedEnd,
                              int row,
                              int rowEnd,
                              List<(int Published, int Raw)> pairs)
    {
        while (published < publishedEnd && row < rowEnd)
        {
            if (publishedNames[published] == rawNames[row])
            {
                pairs.Add((published, row));

                published++;

                row++;

                continue;
            }

            var skip = Resync(publishedNames, rawNames, published, publishedEnd, row, rowEnd);

            published += skip.Published;

            row += skip.Rows;
        }
    }

    private static (int Published, int Rows) Resync(List<string> publishedNames,
                                                    List<string> rawNames,
                                                    int published,
                                                    int publishedEnd,
                                                    int row,
                                                    int rowEnd)
    {
        for (var distance = 1; distance <= Lookahead; distance++)
        {
            if (published + distance < publishedEnd && publishedNames[published + distance] == rawNames[row])
            {
                return (distance, 0);
            }

            if (row + distance < rowEnd && publishedNames[published] == rawNames[row + distance])
            {
                return (0, distance);
            }
        }

        return (1, 1);
    }

    internal sealed record Result(IReadOnlyList<TraceEventLink> Links, int Published, int Written, int Paired);
}
