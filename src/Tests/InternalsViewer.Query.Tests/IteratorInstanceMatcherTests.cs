using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.CallStack.TimeTravel.Iterators;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class IteratorInstanceMatcherTests
{
    [Fact]
    public void Iterators_Of_One_Class_Are_Matched_To_Their_Own_Plan_Nodes()
    {
        var tree = new CallStackTree();

        var join = tree.AddCall(tree.Root, Iterator("CQScanNLJoinNew", "GetRow", 0x100, "Nested Loops"), 6);

        Seek(tree, join, profile: 0x200, seek: 0x300, rows: 5);
        Seek(tree, join, profile: 0x400, seek: 0x500, rows: 7);

        var loops = Operator(0, "Nested Loops");
        var outer = Operator(1, "Index Seek", parent: 0);
        var inner = Operator(2, "Clustered Index Seek", parent: 0);

        var matched = Match(tree, [loops, outer, inner]);

        Assert.Equal(3, matched);
        Assert.Equal(0x100ul, Assert.Single(loops.EntryFrames).Frame?.Instance);
        Assert.Equal(0x300ul, Assert.Single(outer.EntryFrames).Frame?.Instance);
        Assert.Equal(0x500ul, Assert.Single(inner.EntryFrames).Frame?.Instance);
    }

    [Fact]
    public void An_Operator_Exits_Where_Its_Children_Are_Entered()
    {
        var tree = new CallStackTree();

        var join = tree.AddCall(tree.Root, Iterator("CQScanNLJoinNew", "GetRow", 0x100, "Nested Loops"), 6);

        Seek(tree, join, profile: 0x200, seek: 0x300, rows: 5);
        Seek(tree, join, profile: 0x400, seek: 0x500, rows: 7);

        var loops = Operator(0, "Nested Loops");

        Match(tree, [loops, Operator(1, "Index Seek", parent: 0), Operator(2, "Index Seek", parent: 0)]);

        Assert.Equal([0x300ul, 0x500ul], loops.ExitFrames.Select(f => f.Frame!.Instance).Order());
    }

    [Fact]
    public void Every_Call_Into_An_Instance_From_Outside_It_Is_An_Entry()
    {
        var tree = new CallStackTree();

        var join = tree.AddCall(tree.Root, Iterator("CQScanNLJoinNew", "Open", 0x100, "Nested Loops"), 1);

        tree.AddCall(join, Iterator("CQScanRangeNew", "Open", 0x300, "*Index Seek"), 1);

        var getRow = tree.AddCall(tree.Root, Iterator("CQScanNLJoinNew", "GetRow", 0x100, "Nested Loops"), 4);

        tree.AddCall(getRow, Iterator("CQScanNew", "GetRowOrReQualifyHelper", 0x300), 4);

        var seek = Operator(1, "Index Seek", parent: 0);

        Match(tree, [Operator(0, "Nested Loops"), seek]);

        Assert.Equal(["CQScanRangeNew::Open", "CQScanNew::GetRowOrReQualifyHelper"], seek.EntryFrames.Select(f => f.Symbol));
    }

    [Fact]
    public void Producers_On_Worker_Threads_Belong_To_Their_Parallelism_Node()
    {
        var tree = new CallStackTree();

        var query = tree.AddCall(tree.Root, Wrapper("CQueryScan", "GetRow", 0x10, "Iterator"), 2);

        tree.AddCall(query, Iterator("CQScanExchangeNew", "GetRow", 0x100, "Parallelism"), 11);

        foreach (var (producer, scan) in new[] { (0x200ul, 0x300ul), (0x400ul, 0x500ul) })
        {
            var open = tree.AddCall(tree.Root, Iterator("CQScanXProducerNew", "Open", producer, "Parallelism"), 1);

            tree.AddCall(open, Iterator("CQScanTableScanNew", "GetRow", scan, "Table Scan"), 6);
        }

        var gather = Operator(0, "Parallelism");
        var tableScan = Operator(1, "Table Scan", parent: 0);

        Match(tree, [gather, tableScan]);

        Assert.Equal([0x100ul, 0x200ul, 0x400ul], gather.EntryFrames.Select(f => f.Frame!.Instance).Order());
        Assert.Equal([0x300ul, 0x500ul], tableScan.EntryFrames.Select(f => f.Frame!.Instance).Order());
    }

    [Fact]
    public void A_Plan_Node_With_No_Iterator_Of_Its_Own_Is_Passed_Over()
    {
        var tree = new CallStackTree();

        tree.AddCall(tree.Root, Iterator("CQScanIndexNew", "GetRow", 0x100, "*Index Scan"), 3);

        var compute = Operator(0, "Compute Scalar");
        var scan = Operator(1, "Index Scan", parent: 0);

        Match(tree, [compute, scan]);

        Assert.Empty(compute.EntryFrames);
        Assert.Equal(0x100ul, Assert.Single(scan.EntryFrames).Frame?.Instance);
    }

    [Fact]
    public void The_Statement_Is_Entered_Where_It_Runs_The_Plan()
    {
        var tree = new CallStackTree();

        var execute = tree.AddCall(tree.Root, Statement("CXStmtQuery", "ErsqExecuteQuery", "SELECT"), 1);

        var query = tree.AddCall(execute, Wrapper("CQueryScan", "GetRow", 0x10, "Iterator"), 2);

        tree.AddCall(query, Iterator("CQScanRangeNew", "GetRow", 0x100, "*Index Seek"), 2);

        var select = Operator(-1, "SELECT");
        var seek = Operator(0, "Clustered Index Seek", parent: -1);

        Match(tree, [select, seek]);

        Assert.Equal("CXStmtQuery::ErsqExecuteQuery", Assert.Single(select.EntryFrames).Symbol);
        Assert.Equal(0x100ul, Assert.Single(select.ExitFrames).Frame?.Instance);
    }

    [Fact]
    public void An_Iterator_From_Another_Plan_Is_Not_Matched()
    {
        var tree = new CallStackTree();

        var join = tree.AddCall(tree.Root, Iterator("CQScanNLJoinNew", "GetRow", 0x100, "Nested Loops"), 6);

        Seek(tree, join, profile: 0x200, seek: 0x300, rows: 5);
        Seek(tree, join, profile: 0x400, seek: 0x500, rows: 7);

        tree.AddCall(tree.Root, Iterator("CQScanRangeNew", "GetRow", 0x900, "*Index Seek"), 2);

        var outer = Operator(1, "Index Seek", parent: 0);
        var inner = Operator(2, "Index Seek", parent: 0);

        Match(tree, [Operator(0, "Nested Loops"), outer, inner]);

        Assert.DoesNotContain(0x900ul, outer.EntryFrames.Concat(inner.EntryFrames).Select(f => f.Frame!.Instance));
    }

    [Fact]
    public void Only_The_Best_Fitting_Tree_Claims_A_Serial_Plan()
    {
        var tree = new CallStackTree();

        tree.AddCall(tree.Root, Iterator("CQScanRangeNew", "GetRow", 0x900, "*Index Seek"), 2);

        var join = tree.AddCall(tree.Root, Iterator("CQScanNLJoinNew", "GetRow", 0x100, "Nested Loops"), 6);

        Seek(tree, join, profile: 0x200, seek: 0x300, rows: 5);

        var loops = Operator(0, "Nested Loops");

        Match(tree, [loops, Operator(1, "Index Seek", parent: 0)]);

        Assert.Equal(0x100ul, Assert.Single(loops.EntryFrames).Frame?.Instance);
    }

    private static int Match(CallStackTree tree, IReadOnlyList<ExecutionOperatorEvent> operators)
        => IteratorInstanceMatcher.Match(tree.CollapseToFunctions(), operators);

    private static void Seek(CallStackTree tree, CallStackNode parent, ulong profile, ulong seek, long rows)
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

    private static CallstackFrame Iterator(string className, string method, ulong instance, string? planOperator = null)
        => Resolved("sqlmin", className, method, instance, planOperator, planOperator);

    private static CallstackFrame Wrapper(string className, string method, ulong instance, string iterator)
        => Resolved("sqlmin", className, method, instance, iterator, planOperator: null);

    private static CallstackFrame Statement(string className, string method, string planOperator)
        => Resolved("sqllang", className, method, instance: 0, planOperator, planOperator);

    private static CallstackFrame Frame(string className, string method)
        => Resolved("sqlmin", className, method, instance: 0, iterator: null, planOperator: null);

    private static CallstackFrame Resolved(string module,
                                           string className,
                                           string method,
                                           ulong instance,
                                           string? iterator,
                                           string? planOperator) => new()
    {
        Module = module,
        Rva = (uint)StringComparer.Ordinal.GetHashCode($"{className}::{method}"),
        Instance = instance,
        Resolved = new ResolvedCallstackFrame
        {
            ClassName = className,
            MethodName = method,
            SymbolCategory = iterator is null ? SymbolCategory.Unknown : SymbolCategory.QueryOperator,
            Iterator = iterator,
            PlanOperator = planOperator is null ? [] : [GlobPattern.Parse(planOperator)],
        },
    };
}
