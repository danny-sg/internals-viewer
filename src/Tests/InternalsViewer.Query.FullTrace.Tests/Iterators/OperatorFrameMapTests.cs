using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.FullTrace.Symbols.Categories;

namespace InternalsViewer.Query.FullTrace.Tests.Iterators;

[Trait("Category", "Unit")]
public class OperatorFrameMapTests
{
    [Fact]
    public void An_Entry_Frame_Shows_The_Table_And_Index_Of_Its_Operator()
    {
        var tree = new CallTree();

        var open = tree.AddCall(tree.Root, Frame("CQScanRangeNew", "Open", 0x10), 1);

        Label((Operator(1, "Index Scan", "HeapTable", "ix_HeapTable_Id"), open));

        Assert.True(open.HasTarget);
        Assert.Equal("HeapTable.ix_HeapTable_Id", open.TargetLabel);
        Assert.Equal("Query Operator · Index Scan (Node 1) HeapTable.ix_HeapTable_Id", open.TargetDetail);
    }

    [Fact]
    public void A_Frame_Entered_By_Two_Operators_Shows_Both_Tables()
    {
        var tree = new CallTree();

        var getRow = tree.AddCall(tree.Root, Frame("CQScanRowsetNew", "GetRowWithPrefetch", 0x10), 10);

        Label((Operator(1, "Clustered Index Scan", "ClusteredTable", "pk_ClusteredTable"), getRow),
              (Operator(2, "Index Scan", "HeapTable", "ix_HeapTable_Id"), getRow));

        Assert.Equal("ClusteredTable.pk_ClusteredTable, HeapTable.ix_HeapTable_Id", getRow.TargetLabel);
    }

    [Fact]
    public void An_Operator_Without_A_Table_Leaves_No_Label()
    {
        var tree = new CallTree();

        var open = tree.AddCall(tree.Root, Frame("CQScanHash", "Open", 0x10), 1);

        var hash = Operator(0, "Hash Match", tableName: "", indexName: "");

        Label((hash, open));

        Assert.Same(hash, Assert.Single(open.PlanOperators));
        Assert.False(open.HasTarget);
    }

    [Fact]
    public void A_Projected_Tree_Keeps_The_Labels()
    {
        var tree = new CallTree();

        var open = tree.AddCall(tree.Root, Frame("CQScanRangeNew", "Open", 0x10), 1);

        Label((Operator(1, "Index Scan", "HeapTable", "ix_HeapTable_Id"), open));

        var projected = Assert.Single(tree.CollapseToFunctions().Root.ChildNodes);

        Assert.Equal("HeapTable.ix_HeapTable_Id", projected.TargetLabel);
    }

    private static void Label(params (ExecutionOperatorEvent Operator, CallTreeNode Frame)[] entries)
    {
        var frames = new OperatorFrameMap();

        foreach (var (operatorEvent, frame) in entries)
        {
            frames.SetEntryFrames(operatorEvent, [frame]);
        }

        frames.LabelEntryFrames(entries.Select(e => e.Operator));
    }

    private static ExecutionOperatorEvent Operator(int node,
                                                   string physicalOperator,
                                                   string tableName,
                                                   string indexName) => new()
    {
        Name = physicalOperator,
        OperatorDescription = physicalOperator,
        OperatorTableName = tableName,
        OperatorIndexName = indexName,
        PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 1, NodeId = node }
    };

    private static CallFrame Frame(string className, string method, uint rva) => new()
    {
        Module = new("sqlmin"),
        Rva = rva,
        Function = new FrameFunction
        {
            ClassName = className,
            MethodName = method,
            SymbolCategory = SymbolCategory.QueryOperator
        }
    };
}
