using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class IteratorPlanLinkerTests
{
    private const ulong Query = 0x1F4_0000_1000;

    private const ulong OtherQuery = 0x1F4_0000_2000;

    [Fact]
    public void Each_Iterator_Takes_The_Plan_Node_It_Reports()
    {
        var link = Link([Node(0xA1, 0xA0, 0), Node(0xB1, 0xB0, 1), Node(0xC1, 0xC0, 2)],
                        Operator(1, 0, "Hash Match"),
                        Operator(1, 1, "Table Scan"),
                        Operator(1, 2, "Index Seek"));

        Assert.Equal([(0xA1ul, 0), (0xB1ul, 1), (0xC1ul, 2)], NodesOf(link.Instances));
        Assert.Equal([(0xA0ul, 0), (0xB0ul, 1), (0xC0ul, 2)], NodesOf(link.PlanObjects));
    }

    [Fact]
    public void An_Iterator_Whose_Node_Is_Not_In_The_Plan_Is_Not_Linked()
    {
        var link = Link([Node(0xA1, 0xA0, 0), Node(0xB1, 0xB0, 4)], Operator(1, 0, "Hash Match"));

        Assert.Equal([(0xA1ul, 0)], NodesOf(link.Instances));
    }

    [Fact]
    public void Only_The_First_Query_Is_Linked()
    {
        var link = Link([Node(0xA1, 0xA0, 0), Node(0xB1, 0xB0, 0, OtherQuery)], Operator(1, 0, "Table Scan"));

        Assert.Equal([(0xA1ul, 0)], NodesOf(link.Instances));
    }

    [Fact]
    public void The_Statements_Plan_Is_Linked()
    {
        var link = Link([Node(0xA1, 0xA0, 0)], Operator(1, 0, "Table Scan"), Operator(2, -1, "SELECT"), Operator(2, 0, "Hash Match"));

        Assert.Equal((short)2, link.GetNode(0xA1)?.PlanHandleId);
    }

    [Fact]
    public void A_Batch_Iterator_Links_Both_Of_Its_Plan_Objects()
    {
        var link = Link([Node(0xA1, 0xA0, 1, batchPlanObject: 0xAB)], Operator(1, 1, "Filter"));

        Assert.Equal([(0xA0ul, 1), (0xABul, 1)], NodesOf(link.PlanObjects));
    }

    [Fact]
    public void A_Batch_Helper_Is_An_Adapter_Not_The_Operator_It_Reports()
    {
        var link = Link([Node(0xA1, 0xA0, 1, batchPlanObject: 0xAB), Node(0xC1, 0xA0, 1) with { IsBatchHelper = true }],
                        Operator(1, 1, "Filter"));

        Assert.Equal((1, null), ((int?)link.GetNode(0xA1)?.NodeId, link.GetNode(0xC1)));
    }

    [Fact]
    public void A_Batch_Helper_Is_Not_Linked_When_Its_Batch_Operator_Reported_No_Plan_Node()
    {
        var link = Link([Node(0xC1, 0xA0, 1) with { IsBatchHelper = true }, Node(0xD1, 0xD0, 2)],
                        Operator(1, 1, "Filter"),
                        Operator(1, 2, "Top"));

        Assert.Null(link.GetNode(0xC1));
    }

    [Fact]
    public void Plan_Nodes_Survive_Export()
    {
        IteratorPlanNode[] planNodes = [Node(0xA1, 0xA0, 1, batchPlanObject: 0xAB) with { Constructed = new TracePoint(new(5, 6), 7) },
                                        Node(0xC1, 0xC0, 2) with { IsBatchHelper = true }];

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            IteratorPlanNode.Write(writer, planNodes);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        Assert.Equal(planNodes, IteratorPlanNode.Read(reader));
    }

    [Fact]
    public void Query_Teardowns_Survive_Export()
    {
        QueryTeardown[] teardowns = [new(7, new TracePoint(new(9, 10), 11))];

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            QueryTeardown.Write(writer, teardowns);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        Assert.Equal(teardowns, QueryTeardown.Read(reader));
    }

    private static IteratorPlanLink Link(IteratorPlanNode[] planNodes, params ExecutionOperatorEvent[] operators)
        => IteratorPlanLinker.Link(planNodes, operators);

    private static IteratorPlanNode Node(ulong iterator, ulong planObject, int nodeId, ulong query = Query, ulong batchPlanObject = 0)
        => new(iterator, planObject, batchPlanObject, query, nodeId);

    private static List<(ulong Address, int NodeId)> NodesOf(IReadOnlyDictionary<ulong, PlanNodeIdentifier> links)
        => [.. links.Select(l => (l.Key, (int)l.Value.NodeId)).OrderBy(l => l.Key)];

    private static ExecutionOperatorEvent Operator(short planHandle, short node, string physicalOperator) => new()
    {
        Name = physicalOperator,
        OperatorDescription = physicalOperator,
        PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = planHandle, NodeId = node }
    };
}
