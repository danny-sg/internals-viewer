using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class QueryExecutionCropperTests
{
    [Fact]
    public void The_Query_Is_Rooted_At_Its_Execution_Leaving_Out_Compilation()
    {
        var tree = new CallStackTree();

        var source = tree.AddCall(tree.AddCall(tree.Root, Frame("ThreadEntry", "m", 0x10), 1), Frame("CSQLSource", "Execute", 0x20), 1);

        tree.AddCall(source, Frame("CSQLSource", "Transform", 0x30), 1);

        var execute = tree.AddCall(source, Frame("CXStmtQuery", "ErsqExecuteQuery", 0x40), 1);

        tree.AddCall(execute, Frame("CQScanTableScanNew", "GetRow", 0x50), 5);

        Assert.Same(execute, Assert.Single(QueryExecutionCropper.GetRoots(tree)));
    }

    [Fact]
    public void The_Query_Is_Rooted_At_The_Statement_Leaving_Out_The_Batch_Around_It()
    {
        var tree = new CallStackTree();

        var thread = tree.AddCall(tree.Root, Frame("ThreadEntry", "m", 0x10), 1);

        var batch = tree.AddCall(thread, Frame("CMsqlExecContext", "FExecute", 0x18), 1);

        tree.AddCall(batch, Frame("CXStmtQuery", "XretSchemaChanged", 0x80), 1);

        var statement = tree.AddCall(batch, Frame("CXStmtQuery", "ErsqExecuteQuery", 0x40), 1);

        tree.AddCall(batch, Frame("LockOwner", "RemoveGrantedLockOwner", 0x90), 3);

        Assert.Same(statement, Assert.Single(QueryExecutionCropper.GetRoots(tree)));
    }

    [Fact]
    public void An_Inner_Statement_Stays_Under_The_Statement_That_Ran_It()
    {
        var tree = new CallStackTree();

        var thread = tree.AddCall(tree.Root, Frame("ThreadEntry", "m", 0x10), 1);

        var statement = tree.AddCall(thread, Frame("CXStmtQuery", "ErsqExecuteQuery", 0x40), 1);

        var function = tree.AddCall(statement, Frame("CUdfExecInfo", "Execute", 0x50), 1);

        tree.AddCall(function, Frame("CXStmtQuery", "ErsqExecuteQuery", 0x40), 1);

        Assert.Same(statement, Assert.Single(QueryExecutionCropper.GetRoots(tree)));
    }

    [Fact]
    public void A_Thread_That_Never_Executes_The_Query_Keeps_Its_Own_Root()
    {
        var tree = new CallStackTree();

        tree.AddCall(tree.AddCall(tree.Root, Frame("ThreadEntry", "m", 0x10), 1), Frame("CXStmtQuery", "ErsqExecuteQuery", 0x40), 1);

        var worker = tree.AddCall(tree.Root, Frame("SubprocEntrypoint", "m", 0x60), 1);

        tree.AddCall(worker, Frame("CQScanXProducerNew", "GetRow", 0x70, SymbolCategory.QueryOperator), 3);

        Assert.Equal(["CXStmtQuery::ErsqExecuteQuery", "SubprocEntrypoint::m"],
                     QueryExecutionCropper.GetRoots(tree).Select(n => n.Symbol).Order());
    }

    [Fact]
    public void A_Root_That_Neither_Executes_The_Query_Nor_Runs_The_Plan_Is_Dropped()
    {
        var tree = new CallStackTree();

        tree.AddCall(tree.AddCall(tree.Root, Frame("ThreadEntry", "m", 0x10), 1), Frame("CXStmtQuery", "ErsqExecuteQuery", 0x40), 1);

        tree.AddCall(tree.Root, Frame("CXStmtQuery", "XretSchemaChanged", 0x80), 2);

        tree.AddCall(tree.AddCall(tree.Root, Frame("SystemTaskEntryPoint", "m", 0x90), 1), Frame("CheckpointTask", "Run", 0xA0), 4);

        Assert.Equal(["CXStmtQuery::ErsqExecuteQuery"], QueryExecutionCropper.GetRoots(tree).Select(n => n.Symbol));
    }

    [Fact]
    public void A_Tree_Without_The_Query_Execution_Is_Not_Cropped()
    {
        var tree = new CallStackTree();

        tree.AddCall(tree.Root, Frame("ThreadEntry", "m", 0x10), 1);

        Assert.Empty(QueryExecutionCropper.GetRoots(tree));
    }

    [Fact]
    public void A_Trace_Keeps_Compilation_While_Its_Query_Timeline_Starts_At_The_Execution()
    {
        var tree = new CallStackTree();

        var source = tree.AddCall(tree.Root, Frame("CSQLSource", "Execute", 0x20), 1);

        var transform = tree.AddCall(source, Frame("CSQLSource", "Transform", 0x30), 1);

        var execute = tree.AddCall(source, Frame("CXStmtQuery", "ErsqExecuteQuery", 0x40), 1);

        var builder = new TraceTimeline.Builder();

        builder.Add([Span(0, 0, 100), Span(1, 10, 20), Span(2, 30, 90)]);

        var timeline = builder.Build([new(-1, 0, 0x20, 0, 1), new(0, 0, 0x30, 0, 1), new(0, 0, 0x40, 0, 1)]);

        timeline.MapNodes([source, transform, execute]);

        var trace = new TraceResult(tree, new TraceCallStore.Builder().Build(), timeline);

        Assert.Equal((3L, 1L), (trace.Timeline.SpanCount, trace.QueryTimeline.SpanCount));
    }

    [Fact]
    public void Activity_Is_Spread_Over_The_Window_It_Is_Set_To()
    {
        var tree = new CallStackTree { ActivityFromTrace = true };

        var source = tree.AddCall(tree.Root, Frame("CSQLSource", "Execute", 0x20), 1);

        var transform = tree.AddCall(source, Frame("CSQLSource", "Transform", 0x30), 1);

        var execute = tree.AddCall(source, Frame("CXStmtQuery", "ErsqExecuteQuery", 0x40), 1);

        var scan = tree.AddCall(execute, Frame("CQScanTableScanNew", "GetRow", 0x50), 2);

        var builder = new TraceTimeline.Builder();

        builder.Add([Span(0, 0, 100), Span(1, 10, 20), Span(2, 30, 90), Span(3, 32, 35), Span(3, 80, 85)]);

        var timeline = builder.Build([new(-1, 0, 0x20, 0, 1), new(0, 0, 0x30, 0, 1), new(0, 0, 0x40, 0, 1), new(2, 0, 0x50, 0, 1)]);

        timeline.MapNodes([source, transform, execute, scan]);

        var trace = new TraceResult(tree, new TraceCallStore.Builder().Build(), timeline);

        trace.SetActivityWindow(trace.QueryTimeline);

        Assert.Equal([0, 3, 80], Buckets(execute));

        trace.SetActivityWindow(trace.Timeline);

        Assert.Equal([28, 30, 76], Buckets(execute));
    }

    private static CallSpan Span(int callNodeIndex, ulong start, ulong end) => new(1, start, 1, end, start, end, callNodeIndex, 7, 0, 1);

    private static int[] Buckets(CallStackNode node)
        => [.. Enumerable.Range(0, node.ActivityCounts.Length).Where(i => node.ActivityCounts[i] > 0)];

    private static CallstackFrame Frame(string className,
                                        string methodName,
                                        uint rva,
                                        SymbolCategory category = SymbolCategory.Unknown) => new()
    {
        Module = "sqllang",
        Rva = rva,
        Resolved = new ResolvedCallstackFrame { ClassName = className, MethodName = methodName, SymbolCategory = category }
    };
}
