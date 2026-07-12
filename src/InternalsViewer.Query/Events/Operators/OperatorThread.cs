namespace InternalsViewer.Query.Events.Operators;

/// <summary>
/// A single operator thread: its span (capture-relative microseconds) from query_thread_profile and
/// the rows it processed from the plan's per-thread run-time counters.
/// </summary>
public readonly record struct OperatorThread(int ThreadId, long StartUs, long DurationUs, long RowsProcessed)
{
    public long EndUs => StartUs + DurationUs;
}