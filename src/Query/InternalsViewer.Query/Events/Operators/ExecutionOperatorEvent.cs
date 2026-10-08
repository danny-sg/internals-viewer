using InternalsViewer.Query.Events.Properties;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.Events.Operators;

public sealed partial record ExecutionOperatorEvent : EngineEvent
{
    [EventProperty("Level")]
    public int NodeLevel { get; set; }

    public new OperatorCategory Category { get; set; }

    /// <summary>
    /// Parent operator node id
    /// </summary>
    /// <remarks>
    /// <c>null</c> for a root
    /// </remarks>
    [EventProperty("Parent Node")]
    public int? ParentNodeId { get; set; }

    /// <remarks>
    /// When rows first flow out of this operator (capture-relative microseconds). Equals the start for a streaming operator, but is later
    /// for a blocking one (it must consume its input first).
    /// 
    /// A streaming operator inherits its child's emit time, so a blocking descendant delays the whole chain above it. The span before this
    /// is the consume phase (drawn dimmed).
    /// </remarks>
    [EventProperty("First Row", Type = EventPropertyType.Microseconds)]
    public long EmitStartUs { get; set; }

    public override string Description => OperatorDescription;

    [EventProperty("Operator")]
    public required string OperatorDescription { get; set; }

    public string OperatorObjectName { get; init; } = string.Empty;

    public string OperatorSchemaName { get; init; } = string.Empty;

    public string OperatorTableName { get; init; } = string.Empty;

    public string OperatorIndexName { get; init; } = string.Empty;

    public override string ObjectName => OperatorObjectName;

    public override string SchemaName => OperatorSchemaName;

    public override string TableName => OperatorTableName;

    public override string IndexName => OperatorIndexName;

    /// <summary>
    /// The table (and index) this operator targets
    /// </summary>
    public string TargetLabel =>
        TableName.Length == 0
            ? string.Empty
            : IndexName.Length == 0 ? TableName : $"{TableName}.{IndexName}";

    [EventProperty("Logical Operator")]
    public string LogicalOperator { get; set; } = string.Empty;

    [EventProperty("Build Phase Start", Type = EventPropertyType.Microseconds)]
    public long BuildPhaseTimeUs { get; set; }

    [EventProperty("Build Phase Duration", Type = EventPropertyType.Microseconds)]
    public long BuildPhaseDurationUs { get; set; }

    [EventProperty("Probe Phase Start", Type = EventPropertyType.Microseconds)]
    public long ProbePhaseTimeUs { get; set; }

    [EventProperty("Probe Phase Duration", Type = EventPropertyType.Microseconds)]
    public long ProbePhaseDurationUs { get; set; }

    [EventProperty("Cost", Format = "N4")]
    public double? Cost { get; set; }

    /// <summary>
    /// Sum of rows processed across all threads
    /// </summary>
    /// <remarks>
    /// Used to size data-access (scan/seek) bars by data volume. Zero when unknown.
    /// </remarks>
    [EventProperty("Rows", Type = EventPropertyType.Number)]
    public long RowsProcessed { get; set; }

    /// <remarks>
    /// One entry per <c>query_thread_profile</c> thread for this operator (empty for serial operators
    /// with no profile). For a parallel operator <c>thread_id 0</c> is the coordinator and <c>1..N</c>
    /// the workers, so the worker count (degree of parallelism) is the number of non-zero ids.
    /// </remarks>
    public IReadOnlyList<OperatorThread> Threads { get; set; } = [];

    /// <summary>
    /// Operator start call stack frames
    /// </summary>
    /// <remarks>
    /// Filled by <see cref="OperatorCallStackMatcher"/>
    /// 
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
    /// Operator exit frames
    /// </summary>
    /// <remarks>
    /// The bottom of the segment, and it has to be stated rather than inferred. The obvious derivation — "the segment ends where the next
    /// operator's events begin" — assumes every operator has events, and the relational operators have none: only the data-access leaves
    /// emit anything.
    ///
    /// A Stream Aggregate is found on its Index Scan's stacks and nowhere else, so without knowing where the child begins its segment would swallow the child's whole subtree.
    ///
    /// Excludes this operator's own <see cref="EntryFrames"/>, so a recursive operator (or one merged with a descendant
    /// onto the same node) does not end its segment at its own start.
    /// </remarks>
    public IReadOnlyList<CallStackNode> ExitFrames { get; set; } = [];
}