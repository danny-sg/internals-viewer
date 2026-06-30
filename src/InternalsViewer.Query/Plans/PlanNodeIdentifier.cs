namespace InternalsViewer.Query.Plans;

public sealed record PlanNodeIdentifier
{
    public PlanNodeIdentifier()
    {

    }

    public PlanNodeIdentifier(short planHandleId, int nodeId)
    {
        PlanHandleId = planHandleId;
        NodeId = nodeId;
    }

    /// <summary>Interned id of the owning plan handle (see <see cref="PlanHandleRegistry"/>).</summary>
    public short PlanHandleId { get; set; }

    public int NodeId { get; set; }

    public override string ToString()
    {
        return $"{PlanHandleId}:{NodeId}";
    }
}
