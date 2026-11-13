using InternalsViewer.Query.FullTrace.DataPath;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.Plans.Enums;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Tests.DataPath;

[Trait("Category", "Unit")]
public class DataPathBuilderTests
{
    private const string HeapTable = "HeapTable";

    private const string ClusteredTable = "ClusteredTable";

    [Fact]
    public void A_Passed_Through_Column_Reads_The_Top_Operators_Field()
    {
        var map = Build(JoinStatement(), 1, 3, 5);

        var column = map.Columns[0];

        Assert.Equal("Id", column.Name);

        Assert.Null(column.Definition);

        Assert.Equal([(1, "Id")], Sources(column));
    }

    [Fact]
    public void A_Select_Expression_Reads_The_Fields_It_References()
    {
        var map = Build(JoinStatement(), 1, 3, 5);

        var column = map.Columns[1];

        Assert.Equal("[Expr1004]+[Expr1005]", column.Definition?.Expression);

        Assert.Equal(0, column.Expression?.NodeId);

        Assert.Equal([(1, "Expr1004"), (1, "Expr1005")], Sources(column));
    }

    [Fact]
    public void A_Deferred_Compute_Scalar_Defines_The_Operator_Field_It_Is_Read_Through()
    {
        var map = Build(JoinStatement(), 1, 3, 5);

        var stored = map.GetOutput(1, 1)!;

        var returned = map.GetOutput(1, 2)!;

        Assert.Equal((2, 4), (stored.Expression?.NodeId, returned.Expression?.NodeId));

        Assert.Equal([(3, "TextField")], Sources(stored));

        Assert.Equal([(5, "TextField")], Sources(returned));
    }

    [Fact]
    public void Only_The_Build_Input_Is_Blocking()
    {
        var map = Build(JoinStatement(), 1, 3, 5);

        Assert.True(map.GetOutput(1, 1)!.Inputs[0].IsBlocking);

        Assert.False(map.GetOutput(1, 2)!.Inputs[0].IsBlocking);
    }

    [Fact]
    public void A_Key_The_Operator_Also_Returns_Is_A_Use_Of_Its_Own_Field()
        => Assert.Equal(ColumnUseType.BuildKey, Build(JoinStatement(), 1, 3, 5).GetOutput(1, 0)!.Uses);

    [Fact]
    public void A_Key_The_Operator_Does_Not_Return_Is_A_Field_Of_Its_Own()
    {
        var map = Build(JoinStatement(), 1, 3, 5);

        var key = Assert.Single(map.Roots, r => !r.Node.IsStatement);

        Assert.Equal((1, "Id", ColumnUseType.ProbeKey), (key.Node.NodeId, key.Name, key.Uses));

        Assert.Equal([(5, "Id")], Sources(key));

        Assert.Equal(ColumnUseType.ProbeKey, key.Inputs[0].Use);
    }

    [Fact]
    public void An_Aggregate_Reads_The_Columns_Its_Definition_References()
    {
        var map = Build(AggregateStatement(), 1, 2, 4);

        var sum = map.GetOutput(1, 1)!;

        Assert.Equal("SUM([Id])", sum.Definition?.Expression);

        Assert.Equal([(2, "Id")], Sources(sum));

        Assert.Empty(map.GetOutput(1, 2)!.Inputs);
    }

    [Fact]
    public void A_Sort_Column_Reads_Through_A_Deferred_Compute_Scalar_To_The_Scan()
    {
        var map = Build(AggregateStatement(), 1, 2, 4);

        var sorted = map.GetOutput(2, 1)!;

        Assert.Equal("[Id]/(50)", sorted.Definition?.Expression);

        Assert.Equal(3, sorted.Expression?.NodeId);

        Assert.Equal([(4, "Id")], Sources(sorted));
    }

    [Fact]
    public void Group_And_Order_Columns_Are_Uses_Of_Their_Fields()
    {
        var map = Build(AggregateStatement(), 1, 2, 4);

        Assert.Equal(ColumnUseType.Group, map.GetOutput(1, 0)!.Uses);

        Assert.Equal(ColumnUseType.Order, map.GetOutput(2, 1)!.Uses);
    }

    [Fact]
    public void A_Passed_Through_Output_Resolves_To_The_Field_That_Produces_It()
    {
        var scan = Seek(2, "Clustered Index Scan", ClusteredTable);

        var top = new PlanNode { NodeId = 1, PhysicalOperator = "Top", OutputColumns = scan.OutputColumns, Children = [scan] };

        var map = Build(Statement(top), 1, 2);

        Assert.Same(map.GetOutput(2, 0), map.GetOutput(1, 0));

        Assert.Equal([(2, "Id")], Sources(map.Columns[0]));
    }

    [Fact]
    public void Levels_Follow_The_Plan()
    {
        var map = Build(JoinStatement(), 1, 3, 5);

        Assert.Equal([0, 1, 2, 3], new[] { 0, 1, 2, 3 }.Select(n => map.GetLevel(n) ?? -1));
    }

    private static List<(int NodeId, string Name)> Sources(DataPathField field)
        => [.. field.Inputs.Select(i => (i.Source.Node.NodeId, i.Source.Name))];

    private static DataPathMap Build(PlanNode statement, params int[] operators)
    {
        var nodes = new Dictionary<int, PlanNode>();

        foreach (var node in GetNodes(statement))
        {
            nodes[node.NodeId] = node;
        }

        return DataPathBuilder.Build(statement,
                                     operators,
                                     n => GetOutputs(nodes[n], nodes),
                                     (_, _) => PlanOutputPath.Empty,
                                     new Dictionary<int, PlanOutputRoute>(),
                                     _ => null);
    }

    private static IEnumerable<PlanNode> GetNodes(PlanNode node) => node.Children.SelectMany(c => GetNodes(c).Prepend(c));

    private static List<PlanOutputColumn> GetOutputs(PlanNode node, Dictionary<int, PlanNode> nodes)
    {
        var producer = node.PhysicalOperator == "Top" ? node.Children[0].NodeId : node.NodeId;

        return [.. node.OutputColumns.Select((_, i) => new PlanOutputColumn((ulong)(node.NodeId * 0x100 + i),
                                                                            (ulong)producer,
                                                                            new SqlTypeInfo(0x38, 0, 4, 0, 0, 0))
        {
            ProducerNodeId = nodes.ContainsKey(producer) ? producer : -1
        })];
    }

    private static PlanNode Statement(PlanNode child)
        => new() { NodeId = -1, IsStatement = true, PhysicalOperator = "SELECT", Children = [child] };

    private static PlanNode JoinStatement()
        => Statement(new PlanNode
        {
            NodeId = 0,
            PhysicalOperator = "Compute Scalar",
            OutputColumns = [Column("Id", HeapTable), Column("Expr1003")],
            DefinedValues = [Defined("Expr1003", "[Expr1004]+[Expr1005]", Column("Expr1004"), Column("Expr1005"))],
            Children =
            [
                new PlanNode
                {
                    NodeId = 1,
                    PhysicalOperator = "Hash Match",
                    LogicalOperator = "Inner Join",
                    OutputColumns = [Column("Id", HeapTable), Column("Expr1004"), Column("Expr1005")],
                    ReferencedColumns =
                    [
                        new ColumnUse(Column("Id", HeapTable), ColumnUseType.BuildKey),
                        new ColumnUse(Column("Id", ClusteredTable), ColumnUseType.ProbeKey)
                    ],
                    Children =
                    [
                        Scalar(2, "Expr1004", Seek(3, "Index Seek", HeapTable)),
                        Scalar(4, "Expr1005", Seek(5, "Clustered Index Seek", ClusteredTable))
                    ]
                }
            ]
        });

    private static PlanNode AggregateStatement()
        => Statement(new PlanNode
        {
            NodeId = 1,
            PhysicalOperator = "Stream Aggregate",
            OutputColumns = [Column("Expr1002"), Column("Expr1005"), Column("Expr1013")],
            DefinedValues = [Defined("Expr1005", "SUM([Id])", Column("Id", HeapTable)), Defined("Expr1013", "Count(*)")],
            ReferencedColumns = [new ColumnUse(Column("Expr1002"), ColumnUseType.Group)],
            Children =
            [
                new PlanNode
                {
                    NodeId = 2,
                    PhysicalOperator = "Sort",
                    OutputColumns = [Column("Id", HeapTable), Column("Expr1002")],
                    ReferencedColumns = [new ColumnUse(Column("Expr1002"), ColumnUseType.Order)],
                    Children =
                    [
                        new PlanNode
                        {
                            NodeId = 3,
                            PhysicalOperator = "Compute Scalar",
                            OutputColumns = [Column("Id", HeapTable), Column("Expr1002")],
                            DefinedValues = [Defined("Expr1002", "[Id]/(50)", Column("Id", HeapTable))],
                            Children = [Seek(4, "Table Scan", HeapTable)]
                        }
                    ]
                }
            ]
        });

    private static PlanNode Scalar(int nodeId, string column, PlanNode input)
        => new()
        {
            NodeId = nodeId,
            PhysicalOperator = "Compute Scalar",
            OutputColumns = [Column("Id", input.OutputColumns[0].Table), Column(column)],
            DefinedValues = [Defined(column, "substring([TextField],(1),(3))", Column("TextField", input.OutputColumns[0].Table))],
            Children = [input]
        };

    private static PlanNode Seek(int nodeId, string physicalOperator, string table)
        => new()
        {
            NodeId = nodeId,
            PhysicalOperator = physicalOperator,
            OutputColumns = [Column("Id", table), Column("TextField", table)]
        };

    private static ColumnReference Column(string name, string table = "") => new() { Column = name, Table = table };

    private static DefinedValueInfo Defined(string column, string expression, params ColumnReference[] references)
        => new() { Columns = [Column(column)], Expression = expression, References = references };
}
