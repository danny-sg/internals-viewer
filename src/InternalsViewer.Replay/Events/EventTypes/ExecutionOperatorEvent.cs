using InternalsViewer.Replay.Plans;

namespace InternalsViewer.Replay.Events.EventTypes;

public sealed record ExecutionOperatorEvent : EngineEvent
{
    public int NodeLevel { get; set; }

    public OperatorCategory Category { get; set; }

    public override string Description => $"Node {PlanNodeIdentifier?.NodeId}";

    public long BuildPhaseTimeMs { get; set; }

    public long BuildPhaseDuration { get; set; }

    public long ProbePhaseTimeMs { get; set; }

    public long ProbePhaseDuration { get; set; }

    /// <summary>
    /// The operator's own estimated cost (its subtree cost less the subtree cost of its immediate
    /// children), so a parent doesn't double-count the work of the operators feeding it. <c>null</c>
    /// when the plan carries no cost estimate.
    /// </summary>
    public double? Cost { get; set; }
}