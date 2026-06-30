using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Plans;

namespace InternalsViewer.Query.Tests;

public class EventPlanNodeMatcherTests
{
    private const short PlanHandleId = 1;

    [Fact]
    public void Thread_Profile_Event_Anchors_To_Node_By_NodeId()
    {
        var plan = PlanWith(
            Node(0, "Nested Loops", table: null, index: null),
            Node(1, "Index Seek", table: "Orders", index: "IX_Orders_CustomerId"));

        var threadEvent = new QueryThreadEvent
        {
            Name = "query_thread_profile",
            PlanHandleId = PlanHandleId,
            NodeId = 1
        };

        EventPlanNodeMatcher.Match([threadEvent], [plan]);

        Assert.NotNull(threadEvent.PlanNodeIdentifier);
        Assert.Equal(1, threadEvent.PlanNodeIdentifier!.NodeId);
        Assert.Equal(PlanHandleId, threadEvent.PlanNodeIdentifier.PlanHandleId);
    }

    [Fact]
    public void Storage_Event_Matches_Operator_On_Named_Index()
    {
        var plan = PlanWith(
            Node(0, "Index Seek", table: "[Orders]", index: "[IX_Orders_CustomerId]"),
            Node(1, "Clustered Index Scan", table: "[Orders]", index: "[PK_Orders]"));

        var seekRead = new IoEvent
        {
            Name = "physical_page_read",
            PlanHandleId = PlanHandleId,
            TableName = "Orders",
            IndexName = "IX_Orders_CustomerId"
        };

        EventPlanNodeMatcher.Match([seekRead], [plan]);

        Assert.NotNull(seekRead.PlanNodeIdentifier);
        Assert.Equal(0, seekRead.PlanNodeIdentifier!.NodeId);
    }

    [Fact]
    public void Ambiguous_Index_Is_Disambiguated_By_Timing_Window()
    {
        // Self-join: the same index is seeked by two operators.
        var plan = PlanWith(
            Node(2, "Index Seek", table: "Orders", index: "IX_Orders_CustomerId"),
            Node(5, "Index Seek", table: "Orders", index: "IX_Orders_CustomerId"));

        var start = new DateTime(2026, 6, 21, 10, 0, 0, DateTimeKind.Utc);

        // Node 2 closes early, node 5 closes later (each ran for 10ms).
        var profileNode2 = new QueryThreadEvent
        {
            Name = "query_thread_profile",
            PlanHandleId = PlanHandleId,
            NodeId = 2,
            Timestamp = start.AddMilliseconds(10),
            DurationUs = 10_000
        };

        var profileNode5 = new QueryThreadEvent
        {
            Name = "query_thread_profile",
            PlanHandleId = PlanHandleId,
            NodeId = 5,
            Timestamp = start.AddMilliseconds(30),
            DurationUs = 10_000
        };

        // A read that lands inside node 5's window [20ms, 30ms].
        var read = new IoEvent
        {
            Name = "physical_page_read",
            PlanHandleId = PlanHandleId,
            TableName = "Orders",
            IndexName = "IX_Orders_CustomerId",
            Timestamp = start.AddMilliseconds(25)
        };

        EventPlanNodeMatcher.Match([profileNode2, profileNode5, read], [plan]);

        Assert.NotNull(read.PlanNodeIdentifier);
        Assert.Equal(5, read.PlanNodeIdentifier!.NodeId);
    }

    [Fact]
    public void Unrelated_Object_Is_Left_Unmatched()
    {
        var plan = PlanWith(
            Node(0, "Index Seek", table: "Orders", index: "IX_Orders_CustomerId"));

        var read = new IoEvent
        {
            Name = "physical_page_read",
            PlanHandleId = PlanHandleId,
            TableName = "Customers",
            IndexName = "PK_Customers"
        };

        EventPlanNodeMatcher.Match([read], [plan]);

        Assert.Null(read.PlanNodeIdentifier);
    }

    private static ExecutionPlan PlanWith(params PlanNode[] nodes)
    {
        var plan = new ExecutionPlan(PlanHandleId);

        foreach (var node in nodes)
        {
            plan.Root.Add(node);
            plan.NodesById[node.NodeId] = node;
        }

        return plan;
    }

    private static PlanNode Node(int nodeId, string physicalOp, string? table, string? index)
        => new()
        {
            NodeId = nodeId,
            PhysicalOperator = physicalOp,
            Table = table,
            Index = index
        };
}
