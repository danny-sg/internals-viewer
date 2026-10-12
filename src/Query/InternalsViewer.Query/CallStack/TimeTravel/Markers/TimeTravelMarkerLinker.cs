using InternalsViewer.Query.CallStack.TimeTravel.Native;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using InternalsViewer.Query.Events;

namespace InternalsViewer.Query.CallStack.TimeTravel.Markers;

internal static class TimeTravelMarkerLinker
{
    private const int Lookahead = 64;

    public static Result Link(IReadOnlyList<TimeTravelMarker> markers,
                              IReadOnlyList<EventPublisher> publishers,
                              IReadOnlyList<RawEvent> rawEvents,
                              IEnumerable<EngineEvent> events,
                              TimeTravelTimeline timeline)
    {
        var names = publishers.DistinctBy(p => p.Address).ToDictionary(p => p.Address, p => p.Event);

        if (markers.Where(m => m.Buffer != 0).GroupBy(m => m.Buffer).MaxBy(g => g.Count())?.Key is not { } buffer)
        {
            return new Result([], markers.Count, 0, 0);
        }

        var written = markers.Where(m => m.Buffer == buffer && names.ContainsKey(m.Function))
                             .GroupBy(m => m.Thread)
                             .ToDictionary(g => g.Key, g => g.OrderBy(m => m.Sequence).ThenBy(m => m.Steps).ToList());

        var raw = rawEvents.Where(e => e.SystemThreadId is not null)
                           .GroupBy(e => e.SystemThreadId!.Value)
                           .ToDictionary(g => g.Key, g => g.ToList());

        var eventsBySequence = events.BySequence();

        var links = new List<TimeTravelEventLink>();

        var paired = 0;

        foreach (var (thread, threadMarkers) in written)
        {
            if (!raw.TryGetValue(thread, out var threadRaw))
            {
                continue;
            }

            List<string> markerNames = [.. threadMarkers.Select(m => names[m.Function])];

            List<string> rawNames = [.. threadRaw.Select(r => r.Name)];

            List<double> positions = [.. threadMarkers.Select(m => timeline.PositionOf(m.Sequence, m.Steps))];

            var pairs = new List<(int Marker, int Raw)>();

            Align(markerNames, rawNames, 0, markerNames.Count, 0, rawNames.Count, pairs);

            paired += pairs.Count;

            foreach (var (markerIndex, rawIndex) in pairs)
            {
                if (threadRaw[rawIndex].SequenceId is not { } sequence || !eventsBySequence.TryGetValue(sequence, out var engineEvent))
                {
                    continue;
                }

                var found = threadMarkers[markerIndex];

                links.Add(new TimeTravelEventLink(engineEvent, timeline.NodeOf(found.Node), thread, positions[markerIndex], found.Instructions));
            }
        }

        return new Result([.. links.OrderBy(l => l.Event.TimeUs).ThenBy(l => l.Event.SequenceId)],
                          markers.Count,
                          written.Values.Sum(m => m.Count),
                          paired);
    }

    private static void Align(List<string> markerNames,
                              List<string> rawNames,
                              int marker,
                              int markerEnd,
                              int row,
                              int rowEnd,
                              List<(int Marker, int Raw)> pairs)
    {
        while (marker < markerEnd && row < rowEnd)
        {
            if (markerNames[marker] == rawNames[row])
            {
                pairs.Add((marker, row));

                marker++;

                row++;

                continue;
            }

            var skip = Resync(markerNames, rawNames, marker, markerEnd, row, rowEnd);

            marker += skip.Markers;

            row += skip.Rows;
        }
    }

    private static (int Markers, int Rows) Resync(List<string> markerNames,
                                                  List<string> rawNames,
                                                  int marker,
                                                  int markerEnd,
                                                  int row,
                                                  int rowEnd)
    {
        for (var distance = 1; distance <= Lookahead; distance++)
        {
            if (marker + distance < markerEnd && markerNames[marker + distance] == rawNames[row])
            {
                return (distance, 0);
            }

            if (row + distance < rowEnd && markerNames[marker] == rawNames[row + distance])
            {
                return (0, distance);
            }
        }

        return (1, 1);
    }

    internal sealed record Result(IReadOnlyList<TimeTravelEventLink> Links, int Markers, int Written, int Paired);
}
