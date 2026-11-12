namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

public sealed class PlanOutputPath
{
    public static PlanOutputPath Empty { get; } = new();

    public IReadOnlyList<PlanOutputLink> Links { get; init; } = [];

    internal PlanOutputValueSource? Value { get; init; }

    internal PlanOutputValueSource? Record { get; init; }

    internal IReadOnlyList<(int Start, int Count, Action<PlanOutputLinkReader.Route> Add)> Tails { get; init; } = [];
}
