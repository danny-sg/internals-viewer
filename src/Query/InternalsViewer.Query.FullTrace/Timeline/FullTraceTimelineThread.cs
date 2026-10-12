using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class FullTraceTimelineThread(uint threadId, IReadOnlyList<FullTraceTimelineRow> rows, long calls)
{
    public uint ThreadId { get; } = threadId;

    public IReadOnlyList<FullTraceTimelineRow> Rows { get; } = rows;

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

    public double StartOf(FullTraceTimelineAxis axis)
        => Rows.Where(r => r.Count > 0).Select(r => r.Starts(axis)[0]).DefaultIfEmpty(0).Min();

    public double EndOf(FullTraceTimelineAxis axis)
        => Rows.Where(r => r.Count > 0).Select(r => r.Ends(axis)[^1]).DefaultIfEmpty(0).Max();

    public FullTraceTimelineThread Where(Func<int, bool> include)
    {
        FullTraceTimelineRow[] kept = [.. Rows.Select(r => r.Where(include))];

        return new FullTraceTimelineThread(ThreadId, kept, kept.Sum(r => (long)r.Count));
    }
}
