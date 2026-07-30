using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.Events.Operators;

public sealed record ExecutionOperatorEvent : EngineEvent
{
    public int NodeLevel { get; set; }

    public new OperatorCategory Category { get; set; }

    /// <summary>The node id of this operator's parent in the plan tree; <c>null</c> for a root.</summary>
    public int? ParentNodeId { get; set; }

    /// <summary>
    /// When rows first flow out of this operator (capture-relative microseconds). Equals the start for
    /// a streaming operator, but is later for a blocking one (it must consume its input first); a
    /// streaming operator inherits its child's emit time, so a blocking descendant delays the whole
    /// chain above it. The span before this is the consume phase (drawn dimmed).
    /// </summary>
    public long EmitStartUs { get; set; }

    public override string Description => OperatorDescription;

    public required string OperatorDescription { get; set; }

    // An operator's object identity comes from the plan node (schema/table/index), not an allocation unit, so it is
    // stored here and surfaced through the base name getters.
    public string OperatorObjectName { get; init; } = string.Empty;

    public string OperatorSchemaName { get; init; } = string.Empty;

    public string OperatorTableName { get; init; } = string.Empty;

    public string OperatorIndexName { get; init; } = string.Empty;

    public override string ObjectName => OperatorObjectName;

    public override string SchemaName => OperatorSchemaName;

    public override string TableName => OperatorTableName;

    public override string IndexName => OperatorIndexName;

    /// <summary>The table (and index) this operator targets, for display, or empty when it has no object of its own</summary>
    public string TargetLabel =>
        TableName.Length == 0
            ? string.Empty
            : IndexName.Length == 0 ? TableName : $"{TableName}.{IndexName}";

    /// <summary>
    /// The plan's logical operator name (e.g. "Inner Join"), independent of <see cref="EngineEvent.Name"/>
    /// (the physical operator). Used as the timeline label's second line for operators with no object of
    /// their own (joins, sorts, ...) instead of leaving it blank.
    /// </summary>
    public string LogicalOperator { get; set; } = string.Empty;

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

    /// <summary>
    /// The call-stack frames where this operator starts executing, filled by <see cref="OperatorCallStackMatcher"/>
    /// </summary>
    /// <remarks>
    /// The top of the operator's segment: cutting the call tree here gives its own work rather than the whole path from
    /// the thread start. More than one is normal — under parallelism the operator runs on several workers, so it is
    /// entered from several stacks.
    ///
    /// Empty when no frame could be found, which is expected rather than exceptional: an operator may be inlined
    /// (Compute Scalar often is), and the mapping file's coverage of iterator classes will always be partial. Callers
    /// must fall back to the full path, and should say that they have — a segment silently rooted somewhere arbitrary
    /// is worse than an unsegmented one.
    /// </remarks>
    public IReadOnlyList<CallStackNode> EntryFrames { get; set; } = [];

    /// <summary>
    /// The entry frames of the operators below this one, where its segment ends
    /// </summary>
    /// <remarks>
    /// The bottom of the segment, and it has to be stated rather than inferred. The obvious derivation — "the segment
    /// ends where the next operator's events begin" — assumes every operator has events, and the relational operators
    /// have none: only the data-access leaves emit anything. A Stream Aggregate is found on its Index Scan's stacks and
    /// nowhere else, so without knowing where the child begins its segment would swallow the child's whole subtree.
    ///
    /// Excludes this operator's own <see cref="EntryFrames"/>, so a recursive operator (or one merged with a descendant
    /// onto the same node) does not end its segment at its own start.
    /// </remarks>
    public IReadOnlyList<CallStackNode> ExitFrames { get; set; } = [];
}