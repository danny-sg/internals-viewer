namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceTimelineThread(uint threadId, IReadOnlyList<TraceTimelineRow> rows, long calls)
{
    public uint ThreadId { get; } = threadId;

    public IReadOnlyList<TraceTimelineRow> Rows { get; } = rows;

    public long Calls { get; } = calls;

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

        return new TraceTimelineThread(ThreadId, kept, kept.Sum(r => (long)r.Count));
    }
}
