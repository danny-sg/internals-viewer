using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Iterators;

public sealed record IteratorPlanLink(IReadOnlyDictionary<ulong, PlanNodeIdentifier> PlanObjects,
                                      IReadOnlyDictionary<ulong, PlanNodeIdentifier> Instances)
{
    public static IteratorPlanLink Empty { get; } = new(new Dictionary<ulong, PlanNodeIdentifier>(),
                                                        new Dictionary<ulong, PlanNodeIdentifier>());

    public PlanNodeIdentifier? NodeOf(ulong instance) => Instances.GetValueOrDefault(instance);
}
