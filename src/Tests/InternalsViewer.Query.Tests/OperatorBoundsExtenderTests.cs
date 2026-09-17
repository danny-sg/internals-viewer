using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.Tests;

public class OperatorBoundsExtenderTests
{
    [Fact]
    public void ExtendStarts_Pulls_An_Operator_And_Its_Parent_Back_To_The_Earliest_Attributed_Event()
    {
        var scan = Operator(nodeId: 2, parent: 1, timeUs: 5000, durationUs: 1000);

        var aggregate = Operator(nodeId: 1, parent: null, timeUs: 4500, durationUs: 2000);

        var pool = new ObjectPoolEvent { IsHit = false, TimeUs = 3000, DurationUs = 800, PlanNodeIdentifier = scan.PlanNodeIdentifier };

        var later = new SegmentScanEvent { TimeUs = 5200, PlanNodeIdentifier = scan.PlanNodeIdentifier };

        OperatorBoundsExtender.ExtendStarts([aggregate, scan, pool, later]);

        Assert.Equal(3000, scan.TimeUs);
        Assert.Equal(3000, scan.DurationUs);
        Assert.Equal(3000, aggregate.TimeUs);
        Assert.Equal(3500, aggregate.DurationUs);
    }

    [Fact]
    public void ExtendStarts_Leaves_An_Operator_That_Already_Bounds_Its_Events()
    {
        var scan = Operator(nodeId: 2, parent: null, timeUs: 1000, durationUs: 500);

        var inside = new SegmentScanEvent { TimeUs = 1200, PlanNodeIdentifier = scan.PlanNodeIdentifier };

        OperatorBoundsExtender.ExtendStarts([scan, inside]);

        Assert.Equal(1000, scan.TimeUs);
        Assert.Equal(500, scan.DurationUs);
    }

    private static ExecutionOperatorEvent Operator(int nodeId, int? parent, long timeUs, long durationUs) => new()
    {
        OperatorDescription = $"Node {nodeId}",
        PlanNodeIdentifier = new PlanNodeIdentifier { NodeId = nodeId, PlanHandleId = 1 },
        ParentNodeId = parent,
        TimeUs = timeUs,
        DurationUs = durationUs,
        Category = OperatorCategory.DataAccess,
        Timestamp = new DateTime(2026, 1, 1)
    };
}
