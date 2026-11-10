using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Iterators;

[Trait("Category", "Unit")]
public class OperatorFrameMapBuildTests
{
    [Fact]
    public void Each_Iterator_Takes_Its_Linked_Plan_Node_And_Wrappers_Are_Passed_Over()
    {
        var tree = new CallTree();

        var join = tree.AddCall(tree.Root, Iterator("CQScanNLJoinNew", "GetRow", 0x100, "Nested Loops"), 6);

        Seek(tree, join, profile: 0x200, seek: 0x300, rows: 5);
        Seek(tree, join, profile: 0x400, seek: 0x500, rows: 7);

        var loops = Operator(0, "Nested Loops");
        var outer = Operator(1, "Index Seek", parent: 0);
        var inner = Operator(2, "Index Seek", parent: 0);

        var frames = Match(tree, [loops, outer, inner], (0x100, 0), (0x200, 2), (0x300, 2), (0x400, 1), (0x500, 1));

        Assert.Equal(3, new[] { loops, outer, inner }.Count(o => frames.GetInstances(o).Count > 0));
        Assert.Equal([0x500ul], frames.GetInstances(outer));
        Assert.Equal([0x300ul], frames.GetInstances(inner));
        Assert.Equal(0x300ul, Assert.Single(frames.GetEntryFrames(inner)).Frame?.InstanceAddress);
    }

    [Fact]
    public void An_Operator_Exits_Where_Its_Children_Are_Entered()
    {
        var tree = new CallTree();

        var join = tree.AddCall(tree.Root, Iterator("CQScanNLJoinNew", "GetRow", 0x100, "Nested Loops"), 6);

        Seek(tree, join, profile: 0x200, seek: 0x300, rows: 5);
        Seek(tree, join, profile: 0x400, seek: 0x500, rows: 7);

        var loops = Operator(0, "Nested Loops");

        var frames = Match(tree,
                           [loops, Operator(1, "Index Seek", parent: 0), Operator(2, "Index Seek", parent: 0)],
                           (0x100, 0),
                           (0x300, 1),
                           (0x500, 2));

        Assert.Equal([0x300ul, 0x500ul], frames.GetExitFrames(loops).Select(f => f.Frame!.InstanceAddress).Order());
    }

    [Fact]
    public void Every_Call_Into_An_Instance_From_Outside_It_Is_An_Entry()
    {
        var tree = new CallTree();

        var join = tree.AddCall(tree.Root, Iterator("CQScanNLJoinNew", "Open", 0x100, "Nested Loops"), 1);

        tree.AddCall(join, Iterator("CQScanRangeNew", "Open", 0x300, "*Index Seek"), 1);

        var getRow = tree.AddCall(tree.Root, Iterator("CQScanNLJoinNew", "GetRow", 0x100, "Nested Loops"), 4);

        tree.AddCall(getRow, Iterator("CQScanNew", "GetRowOrReQualifyHelper", 0x300), 4);

        var seek = Operator(1, "Index Seek", parent: 0);

        var frames = Match(tree, [Operator(0, "Nested Loops"), seek], (0x100, 0), (0x300, 1));

        Assert.Equal(["CQScanRangeNew::Open", "CQScanNew::GetRowOrReQualifyHelper"], frames.GetEntryFrames(seek).Select(f => f.Symbol));
    }

    [Fact]
    public void Producers_On_Worker_Threads_Belong_To_Their_Parallelism_Node()
    {
        var tree = new CallTree();

        var query = tree.AddCall(tree.Root, Wrapper("CQueryScan", "GetRow", 0x10, "Iterator"), 2);

        tree.AddCall(query, Iterator("CQScanExchangeNew", "GetRow", 0x100, "Parallelism"), 11);

        foreach (var (producer, scan) in new[] { (0x200ul, 0x300ul), (0x400ul, 0x500ul) })
        {
            var open = tree.AddCall(tree.Root, Iterator("CQScanXProducerNew", "Open", producer, "Parallelism"), 1);

            tree.AddCall(open, Iterator("CQScanTableScanNew", "GetRow", scan, "Table Scan"), 6);
        }

        var gather = Operator(0, "Parallelism");
        var tableScan = Operator(1, "Table Scan", parent: 0);

        var frames = Match(tree, [gather, tableScan], (0x100, 0), (0x200, 0), (0x400, 0), (0x300, 1), (0x500, 1));

        Assert.Equal([0x100ul, 0x200ul, 0x400ul], frames.GetEntryFrames(gather).Select(f => f.Frame!.InstanceAddress).Order());
        Assert.Equal([0x300ul, 0x500ul], frames.GetEntryFrames(tableScan).Select(f => f.Frame!.InstanceAddress).Order());
    }

    [Fact]
    public void A_Plan_Node_With_No_Iterator_Of_Its_Own_Is_Passed_Over()
    {
        var tree = new CallTree();

        tree.AddCall(tree.Root, Iterator("CQScanIndexNew", "GetRow", 0x100, "*Index Scan"), 3);

        var compute = Operator(0, "Compute Scalar");
        var scan = Operator(1, "Index Scan", parent: 0);

        var frames = Match(tree, [compute, scan], (0x100, 1));

        Assert.Empty(frames.GetEntryFrames(compute));
        Assert.Equal(0x100ul, Assert.Single(frames.GetEntryFrames(scan)).Frame?.InstanceAddress);
    }

    [Fact]
    public void The_Statement_Is_Entered_Where_It_Runs_The_Plan()
    {
        var tree = new CallTree();

        var execute = tree.AddCall(tree.Root, Statement("CXStmtQuery", "ErsqExecuteQuery", "SELECT"), 1);

        var query = tree.AddCall(execute, Wrapper("CQueryScan", "GetRow", 0x10, "Iterator"), 2);

        tree.AddCall(query, Iterator("CQScanRangeNew", "GetRow", 0x100, "*Index Seek"), 2);

        var select = Operator(-1, "SELECT");
        var seek = Operator(0, "Clustered Index Seek", parent: -1);

        var frames = Match(tree, [select, seek], (0x100, 0));

        Assert.Equal("CXStmtQuery::ErsqExecuteQuery", Assert.Single(frames.GetEntryFrames(select)).Symbol);
        Assert.Equal(0x100ul, Assert.Single(frames.GetExitFrames(select)).Frame?.InstanceAddress);
    }

    [Fact]
    public void The_Statement_Is_Entered_Through_An_Operator_Below_An_Unmatched_One()
    {
        var tree = new CallTree();

        var execute = tree.AddCall(tree.Root, Statement("CXStmtQuery", "ErsqExecuteQuery", "SELECT"), 1);

        var query = tree.AddCall(execute, Wrapper("CQueryScan", "GetRow", 0x10, "Iterator"), 2);

        tree.AddCall(query, Iterator("CQScanRangeNew", "GetRow", 0x100, "*Index Seek"), 2);

        var select = Operator(-1, "SELECT");
        var compute = Operator(0, "Compute Scalar", parent: -1);
        var seek = Operator(1, "Clustered Index Seek", parent: 0);

        var frames = Match(tree, [select, compute, seek], (0x100, 1));

        Assert.Empty(frames.GetEntryFrames(compute));
        Assert.Equal("CXStmtQuery::ErsqExecuteQuery", Assert.Single(frames.GetEntryFrames(select)).Symbol);
    }

    [Fact]
    public void An_Unlinked_Iterator_Is_Not_Matched()
    {
        var tree = new CallTree();

        var join = tree.AddCall(tree.Root, Iterator("CQScanNLJoinNew", "GetRow", 0x100, "Nested Loops"), 6);

        Seek(tree, join, profile: 0x200, seek: 0x300, rows: 5);
        Seek(tree, join, profile: 0x400, seek: 0x500, rows: 7);

        tree.AddCall(tree.Root, Iterator("CQScanRangeNew", "GetRow", 0x900, "*Index Seek"), 2);

        var outer = Operator(1, "Index Seek", parent: 0);
        var inner = Operator(2, "Index Seek", parent: 0);

        var frames = Match(tree, [Operator(0, "Nested Loops"), outer, inner], (0x100, 0), (0x300, 1), (0x500, 2));

        var entries = frames.GetEntryFrames(outer).Concat(frames.GetEntryFrames(inner));

        Assert.DoesNotContain(0x900ul, entries.Select(f => f.Frame!.InstanceAddress));
    }

    private static OperatorFrameMap Match(CallTree tree,
                                          IReadOnlyList<ExecutionOperatorEvent> operators,
                                          params (ulong InstanceAddress, int Node)[] links)
        => OperatorFrameMap.Build(tree.CollapseToFunctions(), operators, LinkOf(links));

    private static IteratorPlanLink LinkOf(params (ulong InstanceAddress, int Node)[] links)
        => new(new Dictionary<ulong, PlanNodeIdentifier>(),
               links.ToDictionary(l => l.InstanceAddress, l => new PlanNodeIdentifier(1, l.Node)));

    private static void Seek(CallTree tree, CallTreeNode parent, ulong profile, ulong seek, long rows)
    {
        var wrapper = tree.AddCall(parent, Wrapper("CQScanProfileNew", "GetRow", profile, "Profiling"), rows + 1);

        var range = tree.AddCall(wrapper, Iterator("CQScanRangeNew", "GetRow", seek, "*Index Seek"), rows + 1);

        tree.AddCall(range, Frame("IndexDataSetSession", "GetNextRowValuesInternal"), rows);
    }

    private static ExecutionOperatorEvent Operator(int node, string physicalOperator, int? parent = null) => new()
    {
        Name = physicalOperator,
        OperatorDescription = physicalOperator,
        PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 1, NodeId = node },
        ParentNodeId = parent,
    };

    private static CallFrame Iterator(string className, string method, ulong instance, string? planOperator = null)
        => Resolved("sqlmin", className, method, instance, planOperator, planOperator);

    private static CallFrame Wrapper(string className, string method, ulong instance, string iterator)
        => Resolved("sqlmin", className, method, instance, iterator, planOperator: null);

    private static CallFrame Statement(string className, string method, string planOperator)
        => Resolved("sqllang", className, method, instance: 0, planOperator, planOperator);

    private static CallFrame Frame(string className, string method)
        => Resolved("sqlmin", className, method, instance: 0, iterator: null, planOperator: null);

    private static CallFrame Resolved(string module,
                                           string className,
                                           string method,
                                           ulong instance,
                                           string? iterator,
                                           string? planOperator) => new()
    {
        Module = new(module),
        Rva = (uint)StringComparer.Ordinal.GetHashCode($"{className}::{method}"),
        InstanceAddress = instance,
        Function = new FrameFunction
        {
            ClassName = className,
            MethodName = method,
            SymbolCategory = iterator is null ? SymbolCategory.Unknown : SymbolCategory.QueryOperator,
            Iterator = iterator,
            PlanOperator = planOperator is null ? [] : [GlobPattern.Parse(planOperator)],
        },
    };
}
