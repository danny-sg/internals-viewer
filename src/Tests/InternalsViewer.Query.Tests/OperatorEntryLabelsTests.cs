using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class OperatorEntryLabelsTests
{
    [Fact]
    public void An_Entry_Frame_Shows_The_Table_And_Index_Of_Its_Operator()
    {
        var tree = new CallStackTree();

        var open = tree.AddCall(tree.Root, Frame("CQScanRangeNew", "Open", 0x10), 1);

        OperatorEntryLabels.Apply([Operator(1, "Index Scan", "HeapTable", "ix_HeapTable_Id", open)]);

        Assert.True(open.HasTarget);
        Assert.Equal("HeapTable.ix_HeapTable_Id", open.TargetLabel);
        Assert.Equal("Index Scan (Node 1) HeapTable.ix_HeapTable_Id", open.TargetDetail);
    }

    [Fact]
    public void A_Frame_Entered_By_Two_Operators_Shows_Both_Tables()
    {
        var tree = new CallStackTree();

        var getRow = tree.AddCall(tree.Root, Frame("CQScanRowsetNew", "GetRowWithPrefetch", 0x10), 10);

        OperatorEntryLabels.Apply([Operator(1, "Clustered Index Scan", "ClusteredTable", "pk_ClusteredTable", getRow),
                                   Operator(2, "Index Scan", "HeapTable", "ix_HeapTable_Id", getRow)]);

        Assert.Equal("ClusteredTable.pk_ClusteredTable, HeapTable.ix_HeapTable_Id", getRow.TargetLabel);
    }

    [Fact]
    public void An_Operator_Without_A_Table_Leaves_No_Label()
    {
        var tree = new CallStackTree();

        var open = tree.AddCall(tree.Root, Frame("CQScanHash", "Open", 0x10), 1);

        var hash = Operator(0, "Hash Match", tableName: "", indexName: "", open);

        OperatorEntryLabels.Apply([hash]);

        Assert.Same(hash, Assert.Single(open.PlanOperators));
        Assert.False(open.HasTarget);
    }

    [Fact]
    public void A_Projected_Tree_Keeps_The_Labels()
    {
        var tree = new CallStackTree();

        var open = tree.AddCall(tree.Root, Frame("CQScanRangeNew", "Open", 0x10), 1);

        OperatorEntryLabels.Apply([Operator(1, "Index Scan", "HeapTable", "ix_HeapTable_Id", open)]);

        var projected = Assert.Single(tree.CollapseToFunctions().Root.ChildNodes);

        Assert.Equal("HeapTable.ix_HeapTable_Id", projected.TargetLabel);
    }

    private static ExecutionOperatorEvent Operator(int node,
                                                   string physicalOperator,
                                                   string tableName,
                                                   string indexName,
                                                   params CallStackNode[] entryFrames) => new()
    {
        Name = physicalOperator,
        OperatorDescription = physicalOperator,
        OperatorTableName = tableName,
        OperatorIndexName = indexName,
        PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 1, NodeId = node },
        EntryFrames = entryFrames
    };

    private static CallstackFrame Frame(string className, string method, uint rva) => new()
    {
        Module = "sqlmin",
        Rva = rva,
        Resolved = new ResolvedCallstackFrame { ClassName = className, MethodName = method }
    };
}
