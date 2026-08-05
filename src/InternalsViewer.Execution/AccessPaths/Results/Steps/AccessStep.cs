namespace InternalsViewer.Execution.AccessPaths.Results.Steps;

/// <summary>
/// A single observable action taken by an access path
/// </summary>
public abstract partial record AccessStep(AccessPhase AccessPhase)
{
    /// <summary>
    /// Totals as they stood immediately after this step was taken
    /// </summary>
    public AccessCounters Counters { get; init; }

    public int NodeId { get; init; }

    public sealed record Open() : AccessStep(AccessPhase.Ranges);

    public sealed record Close() : AccessStep(AccessPhase.Complete);

    /// <summary>
    /// The access path stopped producing rows
    /// </summary>
    public sealed record Stopped(StopReason Reason) : AccessStep(AccessPhase.Complete);

    public sealed record Truncated(long Count) : AccessStep(AccessPhase.Walk);
}
