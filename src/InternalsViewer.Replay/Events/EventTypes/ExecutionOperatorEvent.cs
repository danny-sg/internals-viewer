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
}