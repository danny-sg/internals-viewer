using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.CallStack.Locators.Enums;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Enums;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;
using InternalsViewer.Query.Plans.Model;
using static InternalsViewer.Query.FullTrace.Tests.TraceCallRows;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TraceEvaluationsTests
{
    private const uint Thread = 7;

    private const ulong Helper = 0x1000;

    private const ulong Compare = 0x2000;

    private const ulong TupleCompare = 0x3000;

    private const ulong FilterIterator = 0x1F4_0000_1000;

    private const ulong ScanIterator = 0x1F4_0000_2000;

    private const int ResultRegister = 2;

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
        new(1, 0, Compare, 0, 3),
        new(0, 0, Compare, 0, 2),
        new(1, 0, TupleCompare, 0, 1)
    ];

    [Theory]
    [InlineData(VariantStatus.Value, 0ul, TraceEvaluationResult.False)]
    [InlineData(VariantStatus.Value, 1ul, TraceEvaluationResult.True)]
    [InlineData(VariantStatus.Value, 0x1_0000_0001ul, TraceEvaluationResult.True)]
    [InlineData(VariantStatus.Null, 0ul, TraceEvaluationResult.Unknown)]
    [InlineData(VariantStatus.Null, 1ul, TraceEvaluationResult.Unknown)]
    public void The_Variant_Status_And_Value_Give_The_Result(VariantStatus status, ulong value, TraceEvaluationResult expected)
        => Assert.Equal(expected, TraceEvaluations.GetResult(new VariantLocator(VariantLocator.Create(0, status, value, 0))));

    [Fact]
    public void A_Value_That_Is_Not_A_Boolean_Has_No_Result()
        => Assert.Null(TraceEvaluations.GetResult(new VariantLocator(VariantLocator.Create(0, VariantStatus.Value, 0xFF, 0))));

    [Fact]
    public void An_Evaluation_Spans_Its_Call_And_Keeps_Its_Result()
    {
        var evaluation = Build().All.Single(e => e.Span.From.Position == At(20));

        Assert.Equal((TraceEvaluationResult.False, At(22)), ((TraceEvaluationResult)evaluation.Value, evaluation.Span.To.Position));
    }

    [Fact]
    public void An_Evaluation_Belongs_To_The_Innermost_Operator_Call_Around_It()
    {
        Assert.Equal([Scan, Scan, Filter], Build().All.OrderBy(e => e.Span.From.Position).Select(e => e.Operator));
    }

    [Fact]
    public void Calls_Without_A_Boolean_Result_Are_Not_Evaluations()
    {
        Assert.Equal(3, Build().Count);
    }

    [Fact]
    public void Expression_Service_Functions_Other_Than_The_Evaluations_Are_Not_Read()
    {
        Assert.DoesNotContain(Build().All, e => e.FunctionAddress == TupleCompare);
    }

    [Fact]
    public void Evaluations_Are_Indexed_By_The_Operator_And_Thread_That_Made_Them()
    {
        var evaluations = Build();

        Assert.Equal((2, 1, null),
                     (evaluations.GetTicks(Scan, Thread)?.Count,
                      evaluations.GetTicks(Filter, Thread)?.Count,
                      evaluations.GetTicks(Scan, Thread + 1)?.Count));
    }

    [Fact]
    public void A_Definition_Ticks_Only_The_Calls_Made_Under_Its_Caller()
    {
        var (timeline, calls, lifetimes) = BuildTrace();

        var definition = Definition(TraceConditionTarget.CalledBy, "CQScanTableScanNew::GetRow");

        var ticks = TraceTickSeries.FromDefinition(timeline, calls, lifetimes, definition);

        Assert.Equal((3, true), (ticks.Count, ticks.All.All(t => ReferenceEquals(t.Operator, Scan))));
    }

    [Fact]
    public void A_Definition_Ticks_Only_The_Calls_Inside_Its_Operator()
    {
        var (timeline, calls, lifetimes) = BuildTrace();

        var ticks = TraceTickSeries.FromDefinition(timeline, calls, lifetimes, Definition(TraceConditionTarget.Operator, "Filter"));

        Assert.Equal((2, true), (ticks.Count, ticks.All.All(t => ReferenceEquals(t.Operator, Filter))));
    }

    private static TraceTickDefinition Definition(TraceConditionTarget target, string value)
        => new("Compares",
               "SqlTsEs",
               "CEsExec::FastCompare1",
               "#FFFFFF",
               1,
               1,
               [new TraceCallCondition(target, null, string.Empty, TraceConditionOperator.Equal, value)]);

    private static TraceTickSeries Build()
    {
        var (timeline, calls, lifetimes) = BuildTrace();

        return TraceEvaluations.Build(timeline, calls, lifetimes);
    }

    private static (TraceTimeline Timeline, TraceCallStore Calls, TraceOperatorLifetimes Lifetimes) BuildTrace()
    {
        CallSpan[] spans =
        [
            Span(0, 10, 60),
            Span(1, 15, 30),
            Span(2, 20, 22),
            Span(2, 24, 26, callIndex: 1),
            Span(2, 27, 28, callIndex: 2),
            Span(3, 40, 42, callIndex: 3),
            Span(3, 44, 46, callIndex: 4),
            Span(4, 16, 17)
        ];

        var builder = new TraceTimeline.Builder();

        builder.Add(spans);

        var timeline = builder.Build(Nodes);

        var tree = new CallStackTree();

        var filter = tree.AddCall(tree.Root, Frame(FilterIterator, "CQScanFilterNew", "GetRow", 0x10), 1);

        var scan = tree.AddCall(filter, Frame(ScanIterator, "CQScanTableScanNew", "GetRow", 0x20), 1);

        var scanCompare = tree.AddCall(scan, Frame(0, "CEsExec", "FastCompare1", 0x30), 3);

        var filterCompare = tree.AddCall(filter, Frame(0, "CEsExec", "FastCompare1", 0x30), 2);

        var tupleCompare = tree.AddCall(scan, Frame(0, "CEsExec", "FastTupleCompare", 0x40), 1);

        CallStackNode[] nodes = [filter, scan, scanCompare, filterCompare, tupleCompare];

        timeline.MapNodes(nodes);

        var store = new TraceCallStore.Builder();

        store.Add(Helper, FilterIterator, Chunk(Call(10, 60, 0)), 1);

        store.Add(Helper, ScanIterator, Chunk(Call(15, 30, 1)), 1);

        store.Add(Compare, 0, Chunk(Call(20, 22, 2), Call(24, 26, 2), Call(27, 28, 2), Call(40, 42, 3), Call(44, 46, 3)), 5);

        store.Add(TupleCompare, 0, Chunk(Call(16, 17, 4)), 1);

        store.AddLocator(Compare, 0, 0, VariantLocator.Create(ResultRegister, VariantStatus.Value, 0, 0), true);

        store.AddLocator(Compare, 0, 1, VariantLocator.Create(ResultRegister, VariantStatus.Null, 0, 0), true);

        store.AddLocator(Compare, 0, 2, VariantLocator.Create(ResultRegister, VariantStatus.Value, 0xFF, 0), true);

        store.AddLocator(Compare, 0, 3, VariantLocator.Create(ResultRegister, VariantStatus.Value, 1, 0), true);

        store.AddLocator(TupleCompare, 0, 0, VariantLocator.Create(ResultRegister, VariantStatus.Value, 0, 0), true);

        var calls = store.Build();

        calls.MapNodes(nodes);

        timeline.SetCalls(calls);

        TraceOperatorLifetime[] lifetimes =
        [
            new(Thread, Filter, 0, [Whole()], MemoryInUseCurve.Empty, timeline.Scale),
            new(Thread, Scan, 1, [Whole()], MemoryInUseCurve.Empty, timeline.Scale)
        ];

        return (timeline, calls, new TraceOperatorLifetimes(lifetimes));
    }

    private static ulong[] Call(ulong start, ulong end, int callNodeIndex)
        => Row(start, flags: Thread | TraceCallColumns.ReturnedFlag, callNodeIndex: callNodeIndex, endSequence: end);

    private static CallstackFrame Frame(ulong instance, string className, string method, uint rva) => new()
    {
        Module = "SqlTsEs",
        Rva = rva,
        Address = Helper,
        InstanceAddress = instance,
        Resolved = new ResolvedCallstackFrame
        {
            ClassName = className,
            MethodName = method,
            SymbolCategory = SymbolCategory.Unknown
        }
    };

    private static TraceSpan Whole() => new(new TracePoint(At(0), 0), new TracePoint(At(1000), 1000), 0, 0, TraceSpanFlags.None);

    private static CallSpan Span(int callNodeIndex, ulong start, ulong end, uint callIndex = 0)
        => new(start, 0, end, 0, start, end, callNodeIndex, Thread, callIndex, 1);
}
