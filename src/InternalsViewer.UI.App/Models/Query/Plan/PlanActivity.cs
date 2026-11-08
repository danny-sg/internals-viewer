using System.Collections.Generic;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.UI.App.Models.Query.Plan;

public sealed record PlanActivity(IReadOnlyList<PlanNode> Requested,
                                  IReadOnlyList<PlanNode> Returned,
                                  IReadOnlyList<PlanPartActivity> Parts,
                                  IReadOnlyList<PlanCounter> Counters,
                                  IReadOnlyList<PlanPage> Pages)
{
    public static PlanActivity Empty { get; } = new([], [], [], [], []);

    public bool HasActivePart { get; init; }
}
