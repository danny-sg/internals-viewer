using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Evaluations;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Plans.Model;
using static InternalsViewer.Query.FullTrace.Tests.TraceCallRows;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TraceEvaluationsTests
{
    private const uint Thread = 7;

    private const ulong Helper = 0x1000;

    private const ulong Compare = 0x2000;

    private const ulong FilterIterator = 0x1F4_0000_1000;

    private const ulong ScanIterator = 0x1F4_0000_2000;

    private static readonly ExecutionOperatorEvent Filter = new()
    {
        Name = "Filter",
        OperatorDescription = "Filter",
        Instances = [FilterIterator],
        PlanNodeIdentifier = new PlanNodeIdentifier(1, 0)
    };

    private static readonly ExecutionOperatorEvent Scan = new()
    {
        Name = "Table Scan",
        OperatorDescription = "Table Scan",
        Instances = [ScanIterator],
        PlanNodeIdentifier = new PlanNodeIdentifier(1, 1),
        ParentNodeId = 0
    };

    private static readonly CallNode[] Nodes =
    [
        new(-1, 0, Helper, FilterIterator, 1),
        new(0, 0, Helper, ScanIterator, 1),
        new(1, 0, Compare, 0, 2),
        new(0, 0, Compare, 0, 1)
    ];

    private static readonly EvaluationCapture[] Captures =
    [
        new(Compare, 0, TraceEvaluationResult.False),
        new(Compare, 1, TraceEvaluationResult.Unknown),
        new(Compare, 2, TraceEvaluationResult.True),
        new(Compare, 9, TraceEvaluationResult.True)
    ];

    [Fact]
    public void An_Evaluation_Spans_Its_Call_And_Keeps_Its_Result()
    {
        var evaluation = Build().All[0];

        Assert.Equal((TraceEvaluationResult.False, 20d, 22d), (evaluation.Result, evaluation.Position.Start, evaluation.Position.End));
    }

    [Fact]
    public void An_Evaluation_Belongs_To_The_Innermost_Operator_Call_Around_It()
    {
        Assert.Equal([Scan, Scan, Filter], Build().All.Select(e => e.Operator));
    }

    [Fact]
    public void A_Capture_For_A_Call_That_Was_Not_Recorded_Is_Dropped()
    {
        Assert.Equal(3, Build().Count);
    }

    [Fact]
    public void Evaluations_Are_Indexed_By_The_Operator_And_Thread_That_Made_Them()
    {
        var evaluations = Build();

        Assert.Equal((2, 1, null),
                     (evaluations.GetEvaluations(Scan, Thread)?.Count,
                      evaluations.GetEvaluations(Filter, Thread)?.Count,
                      evaluations.GetEvaluations(Scan, Thread + 1)?.Count));
    }

    [Fact]
    public void Captures_Round_Trip_Through_The_Export()
    {
        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            Build().Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        Assert.Equal(Captures, TraceEvaluations.ReadCaptures(reader));
    }

    private static TraceEvaluations Build()
    {
        CallSpan[] spans =
        [
            Span(0, 10, 60),
            Span(1, 15, 30),
            Span(2, 20, 22),
            Span(2, 24, 26, call: 1),
            Span(3, 40, 42, call: 2)
        ];

        var builder = new TraceTimeline.Builder();

        builder.Add(spans);

        var timeline = builder.Build(Nodes);

        var tree = new CallStackTree();

        var filter = tree.AddCall(tree.Root, Frame(FilterIterator, "CQScanFilterNew", "GetRow"), 1);

        var scan = tree.AddCall(filter, Frame(ScanIterator, "CQScanTableScanNew", "GetRow"), 1);

        var scanCompare = tree.AddCall(scan, Frame(0, "CEsExec", "FastCompare1"), 2);

        var filterCompare = tree.AddCall(filter, Frame(0, "CEsExec", "FastCompare1"), 1);

        CallStackNode[] nodes = [filter, scan, scanCompare, filterCompare];

        timeline.MapNodes(nodes);

        var store = new TraceCallStore.Builder();

        store.Add(Helper, FilterIterator, Chunk(Call(10, 60, 0)), 1);

        store.Add(Helper, ScanIterator, Chunk(Call(15, 30, 1)), 1);

        store.Add(Compare, 0, Chunk(Call(20, 22, 2), Call(24, 26, 2), Call(40, 42, 3)), 3);

        var calls = store.Build();

        calls.MapNodes(nodes);

        timeline.SetCalls(calls);

        timeline.SetLifetimes([new TraceOperatorLifetime(Thread, Filter, 0, [Whole()], [Whole()], MemoryInUseCurve.Empty),
                               new TraceOperatorLifetime(Thread, Scan, 1, [Whole()], [Whole()], MemoryInUseCurve.Empty)]);

        return TraceEvaluations.Build(timeline, calls, Captures);
    }

    private static ulong[] Call(ulong start, ulong end, int node)
        => Row(start, flags: Thread | TraceCallColumns.ReturnedFlag, node: node, endSequence: end);

    private static CallstackFrame Frame(ulong instance, string className, string method) => new()
    {
        Module = "sqlmin",
        Rva = 0x10,
        Address = Helper,
        Instance = instance,
        Resolved = new ResolvedCallstackFrame
        {
            ClassName = className,
            MethodName = method,
            SymbolCategory = SymbolCategory.Unknown
        }
    };

    private static TraceSpan Whole() => new(0, 1000, 0, 0, TraceSpanFlags.None);

    private static CallSpan Span(int node, ulong start, ulong end, uint call = 0) => new(start, 0, end, 0, start, end, node, Thread, call, 1);
}
