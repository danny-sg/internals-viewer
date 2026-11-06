using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Iterators.PlanNodes;

[Trait("Category", "Unit")]
public class QueryTeardownTests
{
    private const uint Returned = 1;

    [Fact]
    public void Each_Thread_Ends_Its_Query_Where_The_Teardown_Returns()
    {
        var timeline = Build([Span(0, 0, 100, 7),
                              Span(2, 80, 90, 7),
                              Span(1, 5, 10, 7),
                              Span(0, 0, 60, 9),
                              Span(2, 50, 55, 9)]);

        Assert.Equal([new QueryTeardown(9, new TracePoint(At(1, 55), 55)), new QueryTeardown(7, new TracePoint(At(1, 90), 90))],
                     QueryTeardown.GetTeardowns(timeline));
    }

    [Fact]
    public void A_Teardown_That_Never_Returned_Does_Not_End_The_Query()
    {
        var timeline = Build([Span(0, 0, 100, 7), Span(2, 80, 100, 7, flags: 0)]);

        Assert.Empty(QueryTeardown.GetTeardowns(timeline));
    }

    private static TraceTimeline Build(CallSpan[] spans)
    {
        var tree = new CallTree();

        var execute = tree.AddCall(tree.Root, Frame("CXStmtQuery", "ErsqExecuteQuery", 0x10), 1);

        var scan = tree.AddCall(execute, Frame("CQScanTableScanNew", "GetRow", 0x20), 1);

        var teardown = tree.AddCall(execute, Frame("CQueryScan", "DestroyQuery", 0x30), 1);

        var builder = new TraceTimeline.Builder();

        builder.Add(spans);

        var timeline = builder.Build([new(-1, 0, 0x10, 0, 1), new(0, 0, 0x20, 0, 1), new(0, 0, 0x30, 0, 1)]);

        timeline.MapNodes([execute, scan, teardown]);

        return timeline;
    }

    private static CallSpan Span(int callNodeIndex, ulong start, ulong end, uint thread, uint flags = Returned)
        => new(1, start, 1, end, start, end, callNodeIndex, thread, 0, flags);

    private static CallFrame Frame(string className, string methodName, uint rva) => new()
    {
        Module = new("sqlmin"),
        Rva = rva,
        Function = new FrameFunction { ClassName = className, MethodName = methodName }
    };
}
