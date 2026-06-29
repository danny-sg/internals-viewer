using InternalsViewer.Query.Plans;

namespace InternalsViewer.Query.Events.EventTypes;

public sealed record ExecutionOperatorEvent : EngineEvent
{
    public int NodeLevel { get; set; }

    public OperatorCategory Category { get; set; }

    public override string Description => $"Node {PlanNodeIdentifier?.NodeId}";

    public long BuildPhaseTimeUs { get; set; }

    public long BuildPhaseDurationUs { get; set; }

    public long ProbePhaseTimeUs { get; set; }

    public long ProbePhaseDurationUs { get; set; }

    /// <summary>
    /// The operator's own estimated cost (its subtree cost less the subtree cost of its immediate
    /// children), so a parent doesn't double-count the work of the operators feeding it. <c>null</c>
    /// when the plan carries no cost estimate.
    /// </summary>
    public double? Cost { get; set; }

    /// <summary>
    /// Rows processed at run time (rows read from storage, else rows output), summed across threads.
    /// Used to size data-access (scan/seek) bars by data volume. Zero when unknown.
    /// </summary>
    public long RowsProcessed { get; set; }

    /// <summary>
    /// One entry per <c>query_thread_profile</c> thread for this operator (empty for serial operators
    /// with no profile). For a parallel operator <c>thread_id 0</c> is the coordinator and <c>1..N</c>
    /// the workers, so the worker count (degree of parallelism) is the number of non-zero ids.
    /// </summary>
    public IReadOnlyList<OperatorThread> Threads { get; set; } = [];
}

/// <summary>
/// A single operator thread: its span (capture-relative microseconds) from query_thread_profile and
/// the rows it processed from the plan's per-thread run-time counters.
/// </summary>
public readonly record struct OperatorThread(int ThreadId, long StartUs, long DurationUs, long RowsProcessed)
{
    public long EndUs => StartUs + DurationUs;
}