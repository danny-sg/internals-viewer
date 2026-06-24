namespace InternalsViewer.Query.Plans;

public sealed record PlanNodeIdentifier
{
    public PlanNodeIdentifier()
    {
        
    }

    public PlanNodeIdentifier(string planHandle, int nodeId)
    {
        PlanHandle = planHandle;
        NodeId = nodeId;
    }

    public string PlanHandle { get; set; } = string.Empty;

    public int NodeId { get; set; }

    public override string ToString()
    {
        return $"{PlanHandle}:{NodeId}";
    }
}