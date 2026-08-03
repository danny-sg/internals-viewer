using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.UI.App.Models.Trace;

/// <summary>
/// The identity each operator of a trace stamps onto the steps it produces
/// </summary>
/// <remarks>
/// Plan node ids are preferred because they tie a step back to the operator that produced it, which is what lets a step be traced to a
/// place in the plan. Without a plan any set that is unique within the trace will do, so the operators are simply numbered.
/// </remarks>
public readonly record struct TraceIteratorIds(int Outer, int Inner, int Join)
{
    public static TraceIteratorIds For(PlanNode? outer, PlanNode? inner, PlanNode? join)
    {
        return outer is not null && inner is not null && join is not null
            ? new TraceIteratorIds(outer.NodeId, inner.NodeId, join.NodeId)
            : new TraceIteratorIds(0, 1, 2);
    }

    public static int For(PlanNode? node) => node?.NodeId ?? 0;
}
