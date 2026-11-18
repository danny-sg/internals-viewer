namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

public sealed class PlanOutputPath
{
    public static PlanOutputPath Empty { get; } = new();

    public IReadOnlyList<PlanOutputLink> Links { get; init; } = [];

    public IReadOnlyList<PlanOutputStage> Stages { get; init; } = [];
}
