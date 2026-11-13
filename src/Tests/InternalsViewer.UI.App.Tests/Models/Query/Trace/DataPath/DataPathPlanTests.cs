using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Models.Query.Trace.DataPath;

namespace InternalsViewer.UI.App.Tests.Models.Query.Trace.DataPath;

[Trait("Category", "Unit")]
public class DataPathPlanTests
{
    private static readonly int[] Iterators = [1, 3, 5];

    [Fact]
    public void Tops_Skip_A_Compute_Scalar_With_No_Iterator()
    {
        var plan = new DataPathPlan(Statement());

        Assert.Equal([1], plan.GetTops(IsOperator));
    }

    [Fact]
    public void A_Passed_Through_Column_Reads_The_Top_Operators_Column()
    {
        var plan = new DataPathPlan(Statement());

        var column = plan.GetColumns(IsOperator, GetColumn)[0];

        Assert.Equal("Id", column.Name);

        Assert.Null(column.Expression);

        Assert.Equal([new DataPathInput(1, Output(1, 0))], column.Inputs);
    }

    [Fact]
    public void A_Calculated_Column_Is_Under_Its_Compute_Scalar_With_The_Columns_It_Reads()
    {
        var plan = new DataPathPlan(Statement());

        var column = plan.GetColumns(IsOperator, GetColumn)[1];

        Assert.Equal("Expr1003", column.Name);

        Assert.Equal(0, column.Expression?.NodeId);

        Assert.Equal("[Expr1004]+[Expr1005]", column.Definition?.Expression);

        Assert.Equal([new DataPathInput(1, Output(1, 1)), new DataPathInput(1, Output(1, 2))], column.Inputs);
    }

    [Fact]
    public void An_Expression_Between_Two_Operators_Is_The_Compute_Scalar_Between_Them()
    {
        var plan = new DataPathPlan(Statement());

        Assert.Equal(2, plan.GetExpression(1, 3)?.NodeId);

        Assert.Equal(4, plan.GetExpression(1, 5)?.NodeId);
    }

    [Fact]
    public void Only_The_Hash_Match_Build_Input_Is_Blocking()
    {
        var plan = new DataPathPlan(Statement());

        Assert.True(plan.IsBlockingInput(1, 3));

        Assert.False(plan.IsBlockingInput(1, 5));
    }

    [Fact]
    public void A_Compute_Scalar_Is_Evaluated_Where_Its_Value_Is_Stored_Or_Returned()
    {
        var statement = Statement();

        var plan = new DataPathPlan(statement);

        var computeScalar = statement.Children[0];

        var join = computeScalar.Children[0];

        Assert.Same(join, plan.GetPuller(join.Children[0]));

        Assert.Same(statement, plan.GetPuller(join.Children[1]));

        Assert.Same(statement, plan.GetPuller(computeScalar));
    }

    [Fact]
    public void A_Calculated_Column_Is_Defined_By_Its_Compute_Scalar()
    {
        var plan = new DataPathPlan(Statement());

        Assert.Equal("substring([TextField],(1),(3))", plan.FindDefinition("Expr1004")?.Expression);

        Assert.Null(plan.FindDefinition("TextField"));
    }

    [Fact]
    public void An_Object_Is_Named_By_Schema_Table_And_Index()
        => Assert.Equal("dbo.HeapTable.ix_TextField",
                        DataPathPlan.GetObjectName(new PlanNode { Schema = "dbo", Table = "HeapTable", Index = "ix_TextField" }));

    [Fact]
    public void Levels_Follow_The_Plan()
    {
        var plan = new DataPathPlan(Statement());

        Assert.Equal([0, 1, 2, 3], new[] { 0, 1, 2, 3 }.Select(n => plan.GetLevel(n) ?? -1));
    }

    private static bool IsOperator(int nodeId) => Iterators.Contains(nodeId);

    private static PlanOutputColumn? GetColumn(int nodeId, int index) => Output(nodeId, index);

    private static PlanOutputColumn Output(int nodeId, int index)
        => new((ulong)(nodeId * 0x100 + index), (ulong)nodeId, new SqlTypeInfo(0x38, 0, 4, 0, 0, 0));

    private static PlanNode Statement()
        => new()
        {
            NodeId = -1,
            IsStatement = true,
            PhysicalOperator = "SELECT",
            Children =
            [
                new PlanNode
                {
                    NodeId = 0,
                    PhysicalOperator = "Compute Scalar",
                    OutputColumns = [Column("Id", "HeapTable"), Column("Expr1003")],
                    DefinedValues = [Defined("Expr1003", "[Expr1004]+[Expr1005]", "Expr1004", "Expr1005")],
                    Children =
                    [
                        new PlanNode
                        {
                            NodeId = 1,
                            PhysicalOperator = "Hash Match",
                            LogicalOperator = "Inner Join",
                            OutputColumns = [Column("Id", "HeapTable"), Column("Expr1004"), Column("Expr1005")],
                            Children =
                            [
                                Scalar(2, "Expr1004", Seek(3, "Index Seek", "HeapTable")),
                                Scalar(4, "Expr1005", Seek(5, "Clustered Index Seek", "ClusteredTable"))
                            ]
                        }
                    ]
                }
            ]
        };

    private static PlanNode Scalar(int nodeId, string column, PlanNode input)
        => new()
        {
            NodeId = nodeId,
            PhysicalOperator = "Compute Scalar",
            OutputColumns = [Column("Id", input.OutputColumns[0].Table), Column(column)],
            DefinedValues = [Defined(column, "substring([TextField],(1),(3))", "TextField")],
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

    private static DefinedValueInfo Defined(string column, string expression, params string[] references)
        => new() { Columns = [Column(column)], Expression = expression, References = references };
}
