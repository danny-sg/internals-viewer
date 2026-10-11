namespace InternalsViewer.Query.Events.Operators;

/// <summary>
/// A single operator thread
/// </summary>
/// <remarks>
/// its span (capture-relative microseconds) from query_thread_profile and the rows it processed from the plan's per-thread run-time
/// counters.
/// </remarks>
public readonly record struct OperatorThread(int ThreadId, long StartUs, long DurationUs, long RowsProcessed)
{
    public long EndUs => StartUs + DurationUs;
}