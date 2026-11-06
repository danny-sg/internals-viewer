using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Arguments.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Ticks.Enums;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Locators.Enums;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Enums;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Tests.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.Plans.Model;
using static InternalsViewer.Query.FullTrace.Tests.TraceCallRowHelper;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Symbols.Categories;

namespace InternalsViewer.Query.FullTrace.Tests.Timeline.Tracks;

[Trait("Category", "Unit")]
public class EvaluationTrackTests
{
    private const uint Thread = 7;

    private const ulong Helper = 0x1000;

    private const ulong Compare = 0x2000;

    private const ulong TupleCompare = 0x3000;

    private const ulong FilterIterator = 0x1F4_0000_1000;

    private const ulong ScanIterator = 0x1F4_0000_2000;

    private const int ResultRegister = 2;

    private const string EvaluationsTrack = "Evaluations";

    private const byte FalseValue = 0;

    private static readonly ExecutionOperatorEvent Filter = new()
    {
        Name = "Filter",
        OperatorDescription = "Filter",
        PlanNodeIdentifier = new PlanNodeIdentifier(1, 0)
    };

    private static readonly ExecutionOperatorEvent Scan = new()
    {
        Name = "Table Scan",
        OperatorDescription = "Table Scan",
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

    [Fact]
    public void An_Evaluation_Spans_Its_Call_And_Keeps_Its_Result()
    {
        var evaluation = Build().All.Single(e => e.Span.From.Position == At(20));

        Assert.Equal((FalseValue, At(22)), (evaluation.Value, evaluation.Span.To.Position));
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

    [Fact]
    public void Rules_With_The_Same_Name_Share_One_Track_With_A_Value_For_Each_Rule()
    {
        var track = FromRules(BuildResult(),
                              Definition(TraceConditionTarget.CalledBy, "CQScanTableScanNew::GetRow"),
                              Definition(TraceConditionTarget.Operator, "Filter"));

        var counts = track.Series.GetValueCounts();

        Assert.Equal<byte>([0, 1], track.Values);

        Assert.Equal((3, 2), (counts[0], counts[1]));
    }

    [Fact]
    public void A_Call_Matched_By_Two_Rules_Of_A_Track_Takes_The_First()
    {
        var track = FromRules(BuildResult(),
                              Definition(TraceConditionTarget.CalledBy, "CQScanTableScanNew::GetRow"),
                              Rule("CEsExec::FastCompare1"));

        var counts = track.Series.GetValueCounts();

        Assert.Equal((5, 3, 2), (track.Series.Count, counts[0], counts[1]));
    }

    [Fact]
    public void A_Function_Can_List_Alternatives_And_Wildcards()
    {
        var trace = BuildResult();

        Assert.Equal((5, 6),
                     (TraceTickSeries.FromDefinition(trace, Rule("CEsExec::Missing|CEsExec::FastCompare1")).Count,
                      TraceTickSeries.FromDefinition(trace, Rule("CEsExec::Fast*")).Count));
    }

    [Fact]
    public void A_Locator_Condition_Checks_The_Register_It_Names_Or_Every_Register()
    {
        var trace = BuildResult();

        var anyRegister = Rule("CEsExec::FastCompare1", HasLocator(null, "Variant"));

        var slot = new ArgumentSlot("Value", "bool", ArgumentLocation.Register, 1, "RDX");

        var otherRegister = Rule("CEsExec::FastCompare1", HasLocator(slot, "Variant"));

        Assert.Equal((4, 0),
                     (TraceTickSeries.FromDefinition(trace, anyRegister).Count,
                      TraceTickSeries.FromDefinition(trace, otherRegister).Count));
    }

    [Fact]
    public void A_Track_Made_Of_A_Whole_Source_Keeps_Its_Ticks_And_Values()
    {
        var trace = BuildResult();

        var source = Evaluations(trace);

        trace = trace with { Seeks = source };

        var track = FromRules(trace, SourceRule(2), SourceRule(0));

        Assert.Same(source, track.Series);

        Assert.Equal<byte>([2, 0], track.Values);
    }

    [Fact]
    public void A_Source_Rule_With_Conditions_Keeps_Only_The_Ticks_It_Matches()
    {
        var trace = BuildResult();

        trace = trace with { Seeks = Evaluations(trace) };

        Assert.Equal((1, 0),
                     (TraceTickSeries.FromDefinition(trace, SourceRule(2, InOperator("Filter"))).Count,
                      TraceTickSeries.FromDefinition(trace, SourceRule(0, InOperator("Filter"))).Count));
    }

    private static TraceTickTrackSeries FromRules(TraceResult trace, params TraceTickDefinition[] rules)
        => TraceTickSeries.FromRules(trace, rules, r => TraceTickSeries.FromDefinition(trace, r));

    private static TraceTickDefinition Rule(string function, params TraceCallCondition[] conditions)
        => new("Compares", "SqlTsEs", function, "#FFFFFF", 1, 1, conditions);

    private static TraceTickDefinition SourceRule(byte value, params TraceCallCondition[] conditions)
        => new("Seek", string.Empty, string.Empty, "#FFFFFF", 1, 1, conditions) { Source = TraceTickSourceType.Seek, Value = value };

    private static TraceCallCondition HasLocator(ArgumentSlot? slot, string type)
        => new(TraceConditionTarget.Locator, slot, string.Empty, TraceConditionOperator.Equal, type);

    private static TraceCallCondition InOperator(string name)
        => new(TraceConditionTarget.Operator, null, string.Empty, TraceConditionOperator.Equal, name);

    private static TraceTickDefinition Definition(TraceConditionTarget target, string value)
        => new("Compares",
               "SqlTsEs",
               "CEsExec::FastCompare1",
               "#FFFFFF",
               1,
               1,
               [new TraceCallCondition(target, null, string.Empty, TraceConditionOperator.Equal, value)]);

    private static TraceTickSeries Build() => Evaluations(BuildResult());

    private static TraceTickSeries Evaluations(TraceResult trace)
        => FromRules(trace, [.. TraceTickDefinition.GetPredefined().Where(d => d.Name == EvaluationsTrack)]).Series;

    private static TraceResult BuildResult()
    {
        var (timeline, calls, lifetimes) = BuildTrace();

        return new TraceResult(new CallTree(), calls, timeline) { Lifetimes = lifetimes };
    }

    private static (TraceTimeline Timeline, TraceCallStore Calls, TraceOperatorLifetimeList Lifetimes) BuildTrace()
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

        var tree = new CallTree();

        var filter = tree.AddCall(tree.Root, Frame(FilterIterator, "CQScanFilterNew", "GetRow", 0x10), 1);

        var scan = tree.AddCall(filter, Frame(ScanIterator, "CQScanTableScanNew", "GetRow", 0x20), 1);

        var scanCompare = tree.AddCall(scan, Frame(0, "CEsExec", "FastCompare1", 0x30), 3);

        var filterCompare = tree.AddCall(filter, Frame(0, "CEsExec", "FastCompare1", 0x30), 2);

        var tupleCompare = tree.AddCall(scan, Frame(0, "CEsExec", "FastTupleCompare", 0x40), 1);

        CallTreeNode[] nodes = [filter, scan, scanCompare, filterCompare, tupleCompare];

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
            new(Thread, Filter, 0, [Whole()], MemoryUsageCurve.Empty, timeline.Scale) { Instances = [FilterIterator] },
            new(Thread, Scan, 1, [Whole()], MemoryUsageCurve.Empty, timeline.Scale) { Instances = [ScanIterator] }
        ];

        return (timeline, calls, new TraceOperatorLifetimeList(lifetimes));
    }

    private static ulong[] Call(ulong start, ulong end, int callNodeIndex)
        => Row(start, flags: Thread | TraceCallChunk.ReturnedFlag, callNodeIndex: callNodeIndex, endSequence: end);

    private static CallFrame Frame(ulong instance, string className, string method, uint rva) => new()
    {
        Module = new("SqlTsEs"),
        Rva = rva,
        Address = Helper,
        InstanceAddress = instance,
        Function = new FrameFunction
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
