using InternalsViewer.Query.FullTrace.Memory.BufferPool;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

internal sealed class RecentRowFilter
{
    private Dictionary<uint, RecentRows> Threads { get; } = [];

    public bool IsRecent(uint thread, TracePageAccess access)
    {
        var rows = Threads.GetValueOrDefault(thread);

        if (IsSameRow(rows.Last, access))
        {
            return true;
        }

        Threads[thread] = new RecentRows(access, rows.Last);

        return IsSameRow(rows.Before, access);
    }

    private static bool IsSameRow(TracePageAccess? row, TracePageAccess access)
        => row is { } recent && recent.Page == access.Page && recent.DatabaseId == access.DatabaseId && recent.Offset == access.Offset;

    private readonly record struct RecentRows(TracePageAccess? Last, TracePageAccess? Before);
}
