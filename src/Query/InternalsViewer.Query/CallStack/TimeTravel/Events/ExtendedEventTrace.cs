using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using InternalsViewer.Query.Events;

namespace InternalsViewer.Query.CallStack.TimeTravel.Events;

public static class ExtendedEventTrace
{
    private const string Showplan = "query_post_execution_showplan";

    public static ExtendedEventTraceSummary Summarise(TimeTravelTimeline timeline,
                                                      TimeTravelCallLog log,
                                                      IReadOnlyDictionary<ulong, string> publishers,
                                                      IReadOnlyCollection<ulong> reserves,
                                                      IReadOnlyList<RawEvent> rawEvents)
    {
        var reserveSet = reserves.ToHashSet();

        var calls = new TimeTravelCallListCache(log);

        var publishes = new List<(uint Thread, double Start, double End, string Name)>();

        var buffers = new List<(uint Thread, double Start, ulong Buffer)>();

        foreach (var span in timeline.ResolvedSpans())
        {
            if (span.Node.Frame is not { } frame)
            {
                continue;
            }

            var start = span.StartOf(TimeTravelTimelineAxis.Position);

            if (publishers.TryGetValue(frame.Address, out var name))
            {
                publishes.Add((span.Thread.ThreadId, start, span.EndOf(TimeTravelTimelineAxis.Position), name));
            }
            else if (reserveSet.Contains(frame.Address)
                     && span.Call is >= 0 and var call
                     && calls.Of(frame.Address) is { } logged
                     && call < logged.Count)
            {
                buffers.Add((span.Thread.ThreadId, start, logged[call].Slot(0)));
            }
        }

        var reservesByThread = buffers.GroupBy(b => b.Thread).ToDictionary(g => g.Key, g => ReservesOf(g.OrderBy(b => b.Start)));

        var inside = publishes.Select(p => (Publish: p, Buffers: BuffersWithin(reservesByThread, p.Thread, p.Start, p.End)))
                              .ToList();

        var sessionBuffer = inside.SelectMany(p => p.Buffers)
                                  .GroupBy(b => b)
                                  .OrderByDescending(g => g.Count())
                                  .Select(g => g.Key)
                                  .FirstOrDefault();

        var written = inside.Where(p => p.Buffers.Contains(sessionBuffer))
                            .Select(p => p.Publish)
                            .ToList();

        var threads = timeline.Threads.Select(t => t.ThreadId).ToHashSet();

        var inFile = rawEvents.Where(e => e.SystemThreadId is { } thread && threads.Contains(thread) && e.Name != Showplan).ToList();

        var writtenByName = written.Where(p => p.Name != Showplan).GroupBy(p => p.Name).ToDictionary(g => g.Key, g => (long)g.Count());

        var inFileByName = inFile.GroupBy(e => e.Name).ToDictionary(g => g.Key, g => (long)g.Count());

        var differences = writtenByName.Keys
                                       .Union(inFileByName.Keys)
                                       .Select(n => (Name: n,
                                                     Written: writtenByName.GetValueOrDefault(n),
                                                     InFile: inFileByName.GetValueOrDefault(n)))
                                       .Where(d => d.Written != d.InFile)
                                       .OrderByDescending(d => Math.Abs(d.Written - d.InFile))
                                       .ToList();

        var workers = rawEvents.Where(e => e is { WorkerAddress: not null and not 0, SystemThreadId: not null })
                               .Select(e => (Worker: e.WorkerAddress!.Value, Thread: e.SystemThreadId!.Value))
                               .Distinct()
                               .ToList();

        var tasks = rawEvents.Where(e => e.TaskAddress is not null and not 0).Select(e => e.TaskAddress!.Value).Distinct().Count();

        return new ExtendedEventTraceSummary(publishes.Count,
                                             written.Count,
                                             inFile.Count,
                                             sessionBuffer,
                                             differences,
                                             workers,
                                             tasks);
    }

    private static ThreadReserves ReservesOf(IEnumerable<(uint Thread, double Start, ulong Buffer)> reserves)
    {
        var ordered = reserves.ToList();

        return new ThreadReserves([.. ordered.Select(r => r.Start)], [.. ordered.Select(r => r.Buffer)]);
    }

    private static List<ulong> BuffersWithin(Dictionary<uint, ThreadReserves> reservesByThread, uint thread, double start, double end)
    {
        var found = new List<ulong>();

        if (!reservesByThread.TryGetValue(thread, out var reserves))
        {
            return found;
        }

        for (var index = SortedSearch.FirstAtOrAfter(reserves.Starts, start);
             index < reserves.Starts.Length && reserves.Starts[index] < end;
             index++)
        {
            found.Add(reserves.Buffers[index]);
        }

        return found;
    }

    private sealed record ThreadReserves(double[] Starts, ulong[] Buffers);
}
