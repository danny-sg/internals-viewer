using System.Runtime.CompilerServices;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline.Ticks;

public sealed class TraceTickIndex
{
    internal TraceTickIndex(TraceTimeline timeline,
                            TraceTimelineThread thread,
                            ExecutionOperatorEvent operatorEvent,
                            List<TraceTickSource> sources)
    {
        sources.Sort(static (left, right) => left.From.Position.CompareTo(right.From.Position));

        Timeline = timeline;

        Thread = thread;

        Operator = operatorEvent;

        Scale = timeline.Scale;

        Entries = [.. sources.Select(s => new TraceTickEntry(s.SpanIndex, s.Value))];

        Froms = [.. sources.Select(s => s.From)];

        Tos = [.. sources.Select(s => s.To)];

        Callers = sources.Any(s => s.Caller is not null) ? [.. sources.Select(s => s.Caller)] : null;

        ValueCount = Entries.Select(e => e.Value).Distinct().Count();

        RunningEnds = new TracePoint[Tos.Length];

        for (var index = 0; index < Tos.Length; index++)
        {
            RunningEnds[index] = index > 0 && RunningEnds[index - 1].Position > Tos[index].Position ? RunningEnds[index - 1] : Tos[index];
        }
    }

    public ExecutionOperatorEvent Operator { get; }

    public int Count => Entries.Length;

    public TraceScale Scale { get; }

    public IReadOnlyList<TraceTick> All => [.. Enumerable.Range(0, Count).Select(Get)];

    internal long Bytes => Count * (Unsafe.SizeOf<TraceTickEntry>() + 3L * Unsafe.SizeOf<TracePoint>())
                           + (Callers is null ? 0 : Count * (long)IntPtr.Size);

    private int ValueCount { get; }

    private Dictionary<byte, TracePosition[]>? EndsByValue { get; set; }

    private TraceTimeline Timeline { get; }

    private TraceTimelineThread Thread { get; }

    private TraceTickEntry[] Entries { get; }

    private TracePoint[] Froms { get; }

    private TracePoint[] Tos { get; }

    private TracePoint[] RunningEnds { get; }

    private ExecutionOperatorEvent?[]? Callers { get; }

    public TraceTick Get(int index)
    {
        var entry = Entries[index];

        var store = Thread.Store;

        var callNodeIndex = store.CallNodeIndexes[entry.SpanIndex];

        var span = new TraceSpan(Froms[index],
                                 Tos[index],
                                 callNodeIndex,
                                 store.GetCallIndex(entry.SpanIndex),
                                 (TraceSpanFlags)store.Flags[entry.SpanIndex]);

        return new TraceTick(entry.Value, Thread.ThreadId, Timeline.GetNode(callNodeIndex)!, span, Operator, Callers?[index]);
    }

    public byte GetValue(int index) => Entries[index].Value;

    public bool IsRunning(TracePosition position)
    {
        var index = SortedSearch.FirstAfter<TracePoint, TracePosition>(Froms, position, static p => p.Position) - 1;

        return index >= 0 && RunningEnds[index].Position > position;
    }

    public bool HasEndBetween(TracePosition after, TracePosition upTo, Func<byte, bool> include)
    {
        for (var index = SortedSearch.FirstAfter<TracePoint, TracePosition>(RunningEnds, after, static p => p.Position);
             index < Count && Froms[index].Position <= upTo;
             index++)
        {
            var end = Tos[index].Position;

            if (end > after && end <= upTo && include(Entries[index].Value))
            {
                return true;
            }
        }

        return false;
    }

    public int CountEnded(byte value, TracePosition upTo)
    {
        EndsByValue ??= Enumerable.Range(0, Count)
                                  .GroupBy(i => Entries[i].Value)
                                  .ToDictionary(g => g.Key, g => g.Select(i => Tos[i].Position).Order().ToArray());

        return EndsByValue.TryGetValue(value, out var ends) ? SortedSearch.FirstAfter<TracePosition>(ends, upTo) : 0;
    }

    public TraceTick? GetLatest(TracePosition position)
    {
        var index = SortedSearch.FirstAfter<TracePoint, TracePosition>(Froms, position, static p => p.Position) - 1;

        return index >= 0 ? Get(index) : null;
    }

    public TraceTick? GetNext(TracePosition after, Func<byte, bool> include)
    {
        for (var index = SortedSearch.FirstAfter<TracePoint, TracePosition>(Froms, after, static p => p.Position); index < Count; index++)
        {
            if (include(Entries[index].Value))
            {
                return Get(index);
            }
        }

        return null;
    }

    public TraceTick? GetPrevious(TracePosition before, Func<byte, bool> include)
    {
        var start = SortedSearch.FirstAtOrAfter<TracePoint, TracePosition>(Froms, before, static p => p.Position);

        for (var index = start - 1; index >= 0; index--)
        {
            if (include(Entries[index].Value))
            {
                return Get(index);
            }
        }

        return null;
    }

    public List<TraceTick> GetRunningTicks(TracePosition position) => GetLatestTicks(position, position, isRunning: true);

    public List<TraceTick> GetEndedTicks(TracePosition after, TracePosition upTo) => GetLatestTicks(after, upTo, isRunning: false);

    private List<TraceTick> GetLatestTicks(TracePosition after, TracePosition upTo, bool isRunning)
    {
        var ticks = new List<TraceTick>();

        Span<bool> seen = stackalloc bool[byte.MaxValue + 1];

        for (var index = SortedSearch.FirstAfter<TracePoint, TracePosition>(Froms, upTo, static p => p.Position) - 1;
             index >= 0 && RunningEnds[index].Position > after && ticks.Count < ValueCount;
             index--)
        {
            var end = Tos[index].Position;

            var value = Entries[index].Value;

            if (seen[value] || end <= after || (!isRunning && end > upTo))
            {
                continue;
            }

            seen[value] = true;

            ticks.Add(Get(index));
        }

        return ticks;
    }

    public double GetStart(TraceTimelineAxis axis, int index) => Scale.GetValue(Froms[index], axis);

    public double GetEnd(TraceTimelineAxis axis, int index) => Scale.GetValue(Tos[index], axis);

    public int GetFirstEndingFrom(TraceTimelineAxis axis, double value) => Scale.GetFirstAtOrAfter(RunningEnds, axis, value);

    public int GetFirstStartingAfter(TraceTimelineAxis axis, double value) => Scale.GetFirstAfter(Froms, axis, value);

    public double? GetNextEnd(TraceTimelineAxis axis, double value)
    {
        double? next = null;

        for (var index = Scale.GetFirstAfter(RunningEnds, axis, value); index < Count; index++)
        {
            if (next is { } limit && GetStart(axis, index) >= limit)
            {
                break;
            }

            var end = GetEnd(axis, index);

            if (end > value && (next is null || end < next))
            {
                next = end;
            }
        }

        return next;
    }

    public double? GetPreviousEnd(TraceTimelineAxis axis, double value)
    {
        double? previous = null;

        for (var index = Scale.GetFirstAtOrAfter(Froms, axis, value) - 1; index >= 0; index--)
        {
            if (previous is { } limit && Scale.GetValue(RunningEnds[index], axis) <= limit)
            {
                break;
            }

            var end = GetEnd(axis, index);

            if (end < value && (previous is null || end > previous))
            {
                previous = end;
            }
        }

        return previous;
    }

    public TraceTick? GetNearest(TraceTimelineAxis axis, double value, double tolerance)
    {
        var nearest = -1;

        var best = double.MaxValue;

        for (var index = Scale.GetFirstAfter(Froms, axis, value + tolerance) - 1;
             index >= 0 && Scale.GetValue(RunningEnds[index], axis) >= value - tolerance;
             index--)
        {
            var start = GetStart(axis, index);

            var end = GetEnd(axis, index);

            var distance = value < start ? start - value : value > end ? value - end : 0;

            if (distance <= tolerance
                && (nearest < 0 || IsInside(index, nearest) || (!IsInside(nearest, index) && distance < best)))
            {
                nearest = index;

                best = distance;
            }
        }

        return nearest >= 0 ? Get(nearest) : null;
    }

    internal TraceTickSource GetSource(int index)
        => new(Thread, Entries[index].SpanIndex, Entries[index].Value, Froms[index], Tos[index])
        {
            Operator = Operator,
            Caller = Callers?[index]
        };

    private bool IsInside(int inner, int outer)
        => Froms[outer].Position <= Froms[inner].Position
           && Tos[inner].Position <= Tos[outer].Position
           && (Froms[inner].Position != Froms[outer].Position || Tos[inner].Position != Tos[outer].Position);
}
