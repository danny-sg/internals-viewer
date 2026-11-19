namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceTimelineThread
{
    private const double MaterialFraction = 0.01;

    internal TraceTimelineThread(uint threadId,
                                 TraceThreadClock clock,
                                 TraceSpanStore store,
                                 IReadOnlyList<TraceTimelineRow> rows,
                                 long callCount)
    {
        ThreadId = threadId;
        Clock = clock;
        Store = store;
        Rows = rows;
        CallCount = callCount;
    }

    public uint ThreadId { get; }

    public TraceThreadClock Clock { get; }

    public IReadOnlyList<TraceTimelineRow> Rows { get; }

    public long CallCount { get; }

    internal TraceSpanStore Store { get; }

    public int Depth
    {
        get
        {
            for (var depth = Rows.Count; depth > 0; depth--)
            {
                if (Rows[depth - 1].Count > 0)
                {
                    return depth;
                }
            }

            return 0;
        }
    }

    public TracePoint Start
        => Rows.Where(r => r.Count > 0).Select(r => r.GetFrom(0)).DefaultIfEmpty(default).MinBy(p => p.Position);

    public TracePoint End
        => Rows.Where(r => r.Count > 0).Select(r => r.GetTo(r.Count - 1)).DefaultIfEmpty(default).MaxBy(p => p.Position);

    private TracePosition[]? Boundaries { get; set; }

    public TraceTimelineThread Filter(Func<int, bool> include)
    {
        TraceTimelineRow[] kept = [.. Rows.Select(r => r.Filter(include))];

        return new TraceTimelineThread(ThreadId, Clock, Store, kept, kept.Sum(r => (long)r.Count));
    }

    internal IReadOnlyList<(TracePosition From, TracePosition To)> 
        GetMaterialDeadSpans(IEnumerable<(TracePosition From, TracePosition To)> excluded, TraceScale scale)
    {
        List<(TracePosition From, TracePosition To)> dead = [.. excluded];

        if (dead.Count == 0)
        {
            return [];
        }

        var kept = new TracePosition[Rows.Sum(r => r.Count)];

        var next = 0;

        foreach (var row in Rows)
        {
            for (var index = 0; index < row.Count; index++)
            {
                kept[next++] = row.GetFromPosition(index);
            }
        }

        dead.Sort((left, right) => left.From.CompareTo(right.From));

        Array.Sort(kept);

        var start = Start.Position;

        var end = End.Position;

        var instructionLimit = ((double)Clock.GetRecordedInstructions(end) - Clock.GetRecordedInstructions(start)) * MaterialFraction;

        var positionLimit = (scale.GetRecordedValue(end) - scale.GetRecordedValue(start)) * MaterialFraction;

        List<(TracePosition From, TracePosition To)> merged = [];

        foreach (var (from, to) in dead)
        {
            if (merged.Count > 0
                && (from <= merged[^1].To || (!IsMaterial(merged[^1].To, from) && !HasPositionBetween(kept, merged[^1].To, from))))
            {
                merged[^1] = (merged[^1].From, to > merged[^1].To ? to : merged[^1].To);
            }
            else
            {
                merged.Add((from, to));
            }
        }

        return [.. merged.Where(s => IsMaterial(s.From, s.To))];

        bool IsMaterial(TracePosition from, TracePosition to)
            => (double)Clock.GetRecordedInstructions(to) - Clock.GetRecordedInstructions(from) > instructionLimit
               || scale.GetRecordedValue(to) - scale.GetRecordedValue(from) > positionLimit;
    }

    internal bool HasBoundaryWithin(TracePosition from, TracePosition to)
    {
        Boundaries ??= GetBoundaries();

        return HasPositionBetween(Boundaries, from, to);
    }

    private TracePosition[] GetBoundaries()
    {
        var boundaries = new TracePosition[Rows.Sum(r => 2 * r.Count)];

        var next = 0;

        foreach (var row in Rows)
        {
            for (var index = 0; index < row.Count; index++)
            {
                boundaries[next++] = row.GetFromPosition(index);

                boundaries[next++] = row.GetToPosition(index);
            }
        }

        Array.Sort(boundaries);

        return boundaries;
    }

    private static bool HasPositionBetween(IReadOnlyList<TracePosition> positions, TracePosition from, TracePosition to)
    {
        var low = 0;

        var high = positions.Count;

        while (low < high)
        {
            var middle = low + (high - low) / 2;

            if (positions[middle] <= from)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low < positions.Count && positions[low] < to;
    }
}
