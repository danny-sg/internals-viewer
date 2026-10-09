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

        var calls = new Dictionary<ulong, TimeTravelCallList?>();

        var publishes = new List<(uint Thread, double Start, double End, string Name)>();

        var buffers = new List<(uint Thread, double Start, ulong Buffer)>();

        foreach (var thread in timeline.Threads)
        {
            foreach (var row in thread.Rows)
            {
                var starts = row.Starts(TimeTravelTimelineAxis.Position);

                var ends = row.Ends(TimeTravelTimelineAxis.Position);

                for (var index = 0; index < row.Count; index++)
                {
                    if (timeline.NodeOf(row.NodeAt(index))?.Frame is not { } frame)
                    {
                        continue;
                    }

                    if (publishers.TryGetValue(frame.Address, out var name))
                    {
                        publishes.Add((thread.ThreadId, starts[index], ends[index], name));
                    }
                    else if (reserveSet.Contains(frame.Address)
                             && row.CallAt(index) is >= 0 and var call
                             && CallsOf(log, calls, frame.Address) is { } logged
                             && call < logged.Count
                             && logged[call].IntegerSlots.Length > 0)
                    {
                        buffers.Add((thread.ThreadId, starts[index], logged[call].IntegerSlots[0]));
                    }
                }
            }
        }

        var reservesByThread = buffers.GroupBy(b => b.Thread)
                                      .ToDictionary(g => g.Key, g => g.OrderBy(b => b.Start).ToArray());

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

    private static List<ulong> BuffersWithin(Dictionary<uint, (uint Thread, double Start, ulong Buffer)[]> reservesByThread,
                                             uint thread,
                                             double start,
                                             double end)
    {
        var found = new List<ulong>();

        if (!reservesByThread.TryGetValue(thread, out var reserves))
        {
            return found;
        }

        var low = 0;

        var high = reserves.Length;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            if (reserves[middle].Start < start)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        for (var index = low; index < reserves.Length && reserves[index].Start < end; index++)
        {
            found.Add(reserves[index].Buffer);
        }

        return found;
    }

    private static TimeTravelCallList? CallsOf(TimeTravelCallLog log, Dictionary<ulong, TimeTravelCallList?> calls, ulong address)
    {
        if (!calls.TryGetValue(address, out var logged))
        {
            logged = log.CallsOf(address, 0);

            calls[address] = logged;
        }

        return logged;
    }
}
