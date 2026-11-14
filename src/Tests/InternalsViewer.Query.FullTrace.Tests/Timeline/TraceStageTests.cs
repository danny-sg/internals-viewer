using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using static InternalsViewer.Query.FullTrace.Tests.TraceCallRowHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Timeline;

[Trait("Category", "Unit")]
public class TraceStageTests
{
    private const uint Returned = 1;

    private const ulong SearchAddress = 0x60;

    [Fact]
    public void Stages_Nest_Inside_The_Stages_That_Call_Them()
    {
        var stages = TraceStage.Build(Build([Span(0, 0, 100, 7),
                                             Span(1, 10, 30, 7),
                                             Span(2, 40, 90, 7),
                                             Span(3, 45, 80, 7)]));

        Assert.Equal([("Compilation", 0), ("Algebrization", 1), ("Plan Build", 1), ("Optimization", 2)],
                     stages.Select(s => (s.Name, s.Depth)));
    }

    [Fact]
    public void Other_Calls_Are_Not_Stages()
    {
        var stages = TraceStage.Build(Build([Span(0, 0, 50, 7), Span(4, 60, 90, 7)]));

        Assert.Equal(["Compilation"], stages.Select(s => s.Name));
    }

    [Fact]
    public void A_Stage_On_Another_Thread_Is_Not_Nested()
    {
        var stages = TraceStage.Build(Build([Span(0, 0, 100, 7), Span(1, 10, 30, 9)]));

        Assert.Equal([(7u, 0), (9u, 0)], stages.Select(s => (s.ThreadId, s.Depth)));
    }

    [Fact]
    public void A_Stage_Starting_Where_Another_Ends_Is_Not_Nested()
    {
        var stages = TraceStage.Build(Build([Span(0, 0, 50, 7), Span(0, 50, 90, 7)]));

        Assert.Equal([0, 0], stages.Select(s => s.Depth));
    }

    [Fact]
    public void A_Search_Is_Named_By_Its_Optimization_Level()
    {
        var calls = new TraceCallStore.Builder();

        calls.Add(SearchAddress, 0, Chunk(Row(1, rdx: 1), Row(1, rdx: 2)), 2);

        var stages = TraceStage.Build(Build([Span(3, 0, 100, 7), Span(5, 10, 40, 7), Span(5, 50, 90, 7, callIndex: 1)], calls.Build()));

        Assert.Equal([("Optimization", 0), ("Search 1", 1), ("Search 2", 1)], stages.Select(s => (s.Name, s.Depth)));
    }

    [Fact]
    public void A_Search_Without_Its_Logged_Call_Has_No_Level()
    {
        var stages = TraceStage.Build(Build([Span(5, 10, 40, 7)]));

        Assert.Equal(["Search"], stages.Select(s => s.Name));
    }

    private static TraceTimeline Build(CallSpan[] spans, TraceCallStore? calls = null)
    {
        var tree = new CallTree();

        var compile = tree.AddCall(tree.Root, Frame("sqllang", "CSQLSource", "FCompile", 0x10), 1);

        var normalize = tree.AddCall(compile, Frame("sqllang", "CProchdr", "FNormalizeStep", 0x20), 1);

        var build = tree.AddCall(compile, Frame("sqllang", string.Empty, "PqoBuild", 0x30), 1);

        var optimize = tree.AddCall(build, Frame("sqllang", "COptContext", "PqteOptimizeWrapper", 0x40), 1);

        var scan = tree.AddCall(tree.Root, Frame("sqlmin", "CQScanTableScanNew", "GetRow", 0x50), 1);

        var search = tree.AddCall(optimize, Frame("sqllang", "CMemo", "PerformOptimizationStage", SearchAddress), 2);

        var builder = new TraceTimeline.Builder();

        builder.Add(spans);

        var timeline = builder.Build([new(-1, 0x10, 0, 1),
                                      new(0, 0x20, 0, 1),
                                      new(0, 0x30, 0, 1),
                                      new(2, 0x40, 0, 1),
                                      new(-1, 0x50, 0, 1),
                                      new(3, SearchAddress, 0, 2)]);

        timeline.MapNodes([compile, normalize, build, optimize, scan, search]);

        if (calls is not null)
        {
            timeline.SetCalls(calls);
        }

        return timeline;
    }

    private static CallSpan Span(int callNodeIndex, ulong start, ulong end, uint thread, uint callIndex = 0)
        => new(1, start, 1, end, start, end, callNodeIndex, thread, callIndex, Returned);

    private static CallFrame Frame(string module, string className, string methodName, ulong address) => new()
    {
        Module = new(module),
        Address = address,
        Rva = (uint)address,
        Function = new FrameFunction { ClassName = className, MethodName = methodName }
    };
}
