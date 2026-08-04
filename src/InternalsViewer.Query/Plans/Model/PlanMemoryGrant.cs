namespace InternalsViewer.Query.Plans.Model;

public sealed record PlanMemoryGrant
{
    public long? InputKb { get; init; }

    public long? OutputKb { get; init; }

    public long? UsedKb { get; init; }
}
