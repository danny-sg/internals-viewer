namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceTimelineThread
{
    internal TraceTimelineThread(uint threadId,
                                 TraceThreadClock clock,
                                 TraceSpanStore store,
                                 IReadOnlyList<TraceTimelineRow> rows,
                                 long calls)
    {
        ThreadId = threadId;
        Clock = clock;
        Store = store;
        Rows = rows;
        Calls = calls;
    }

    public uint ThreadId { get; }

    public TraceThreadClock Clock { get; }

    public IReadOnlyList<TraceTimelineRow> Rows { get; }

    public long Calls { get; private set; }

    internal TraceSpanStore Store { get; }

    internal long Bytes => Clock.Bytes + Store.Bytes + Rows.Sum(r => r.Bytes);

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

    public TraceTimelineThread Where(Func<int, bool> include)
    {
        TraceTimelineRow[] kept = [.. Rows.Select(r => r.Where(include))];

        return new TraceTimelineThread(ThreadId, Clock, Store, kept, kept.Sum(r => (long)r.Count));
    }

    internal void RemoveAll(Func<int, bool> isRemoved)
    {
        foreach (var row in Rows)
        {
            Calls -= row.RemoveAll(isRemoved);
        }
    }
}
