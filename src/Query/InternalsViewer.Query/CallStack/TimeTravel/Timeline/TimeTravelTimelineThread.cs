namespace InternalsViewer.Query.CallStack.TimeTravel.Timeline;

public sealed class TimeTravelTimelineThread(uint threadId, IReadOnlyList<TimeTravelTimelineRow> rows, long calls)
{
    public uint ThreadId { get; } = threadId;

    public IReadOnlyList<TimeTravelTimelineRow> Rows { get; } = rows;

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

    public double StartOf(TimeTravelTimelineAxis axis)
        => Rows.Where(r => r.Count > 0).Select(r => r.Starts(axis)[0]).DefaultIfEmpty(0).Min();

    public double EndOf(TimeTravelTimelineAxis axis)
        => Rows.Where(r => r.Count > 0).Select(r => r.Ends(axis)[^1]).DefaultIfEmpty(0).Max();

    public TimeTravelTimelineThread Where(Func<int, bool> include) => From([.. Rows.Select(r => r.Where(include))]);

    private TimeTravelTimelineThread From(TimeTravelTimelineRow[] rows) => new(ThreadId, rows, rows.Sum(r => (long)r.Count));
}
