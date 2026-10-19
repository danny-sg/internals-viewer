using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
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
                        [],
                        Operator(1, 0, "Hash Match"),
                        Operator(1, 1, "Table Scan"),
                        Operator(1, 2, "Index Seek"));

        Assert.Equal([(0xA1ul, 0), (0xB1ul, 1), (0xC1ul, 2)], NodesOf(link.Instances));
        Assert.Equal([(0xA0ul, 0), (0xB0ul, 1), (0xC0ul, 2)], NodesOf(link.PlanObjects));
    }

    [Fact]
    public void A_Query_Is_Not_Linked_To_A_Plan_Without_Its_Nodes()
        => Assert.Same(IteratorPlanLink.Empty, Link([Node(0xA1, 0xA0, 0), Node(0xB1, 0xB0, 4)], [], Operator(1, 0, "Hash Match")));

    [Fact]
    public void A_Query_Takes_The_Plan_Whose_Operators_Match_Its_Iterators()
    {
        var tree = new CallStackTree();

        var nodes = new List<CallStackNode?>
        {
            tree.AddCall(tree.Root, Frame("CQScanNLJoinNew", 0xA1, "Nested Loops"), 1),
            tree.AddCall(tree.Root, Frame("CQScanRangeNew", 0xB1, "*Index Seek"), 1)
        };

        var link = Link([Node(0xA1, 0xA0, 0), Node(0xB1, 0xB0, 1)],
                        nodes,
                        Operator(1, 0, "Hash Match"),
                        Operator(1, 1, "Table Scan"),
                        Operator(2, 0, "Nested Loops"),
                        Operator(2, 1, "Index Seek"));

        Assert.Equal((short)2, link.GetNode(0xB1)?.PlanHandleId);
    }

    [Fact]
    public void Each_Query_Takes_Its_Own_Plan()
    {
        var link = Link([Node(0xA1, 0xA0, 0), Node(0xB1, 0xB0, 0, OtherQuery)],
                        [],
                        Operator(1, 0, "Table Scan"),
                        Operator(2, 0, "Table Scan"));

        Assert.Equal(((short)1, (short)2), (link.GetNode(0xA1)!.PlanHandleId, link.GetNode(0xB1)!.PlanHandleId));
    }

    [Fact]
    public void A_Batch_Iterator_Links_Both_Of_Its_Plan_Objects()
    {
        var link = Link([Node(0xA1, 0xA0, 1, batchPlanObject: 0xAB)], [], Operator(1, 1, "Filter"));

        Assert.Equal([(0xA0ul, 1), (0xABul, 1)], NodesOf(link.PlanObjects));
    }

    [Fact]
    public void A_Row_Iterator_Sharing_A_Batch_Operators_Plan_Object_Is_An_Adapter_Not_That_Operator()
    {
        var link = Link([Node(0xA1, 0xA0, 1, batchPlanObject: 0xAB), Node(0xC1, 0xA0, 1)], [], Operator(1, 1, "Filter"));

        Assert.Null(link.GetNode(0xC1));
    }

    [Fact]
    public void Plan_Nodes_Survive_Export()
    {
        IteratorPlanNode[] planNodes = [Node(0xA1, 0xA0, 1, batchPlanObject: 0xAB), Node(0xC1, 0xC0, 2)];

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            IteratorPlanNode.Write(writer, planNodes);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        Assert.Equal(planNodes, IteratorPlanNode.Read(reader));
    }

    private static IteratorPlanLink Link(IteratorPlanNode[] planNodes, List<CallStackNode?> nodes, params ExecutionOperatorEvent[] operators)
        => IteratorPlanLinker.Link(planNodes, nodes, operators);

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

    private static CallstackFrame Frame(string className, ulong instance, string planOperator) => new()
    {
        Module = "sqlmin",
        Rva = (uint)instance,
        Address = 0x7FF0_0000_0000 + instance,
        Instance = instance,
        Resolved = new ResolvedCallstackFrame
        {
            ClassName = className,
            MethodName = "GetRow",
            SymbolCategory = SymbolCategory.QueryOperator,
            PlanOperator = [.. planOperator.Split(',').Select(GlobPattern.Parse)]
        }
    };
}
