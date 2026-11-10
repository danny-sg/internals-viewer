using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Locators.Enums;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.Plans.Model;
using static InternalsViewer.Query.FullTrace.Tests.TraceCallRowHelper;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;
using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Timeline.Tracks;

[Trait("Category", "Unit")]
public class TraceSeekRangeTrackBuilderTests
{
    private const uint Thread = 7;

    private const int KeyRegister = 1;

    private const ulong GetRowFunction = 0x1000;

    private const ulong SetRangeFunction = 0x2000;

    private const ulong EvaluateFunction = 0x3000;

    private const ulong GeneralEvalFunction = 0x4000;

    private const ulong GetDataFunction = 0x5000;

    private const ulong GetDataFromVariantFunction = 0x6000;

    private const ulong RangeSetFunction = 0x7000;

    private const ulong RangeIterator = 0x1F4_0000_1000;

    private static readonly ExecutionOperatorEvent Seek = new()
    {
        Name = "Clustered Index Seek",
        OperatorDescription = "Clustered Index Seek",
        PlanNodeIdentifier = new PlanNodeIdentifier(1, 0)
    };

    private static readonly ExecutionOperatorEvent SingleRangeSeek = new()
    {
        Name = "Index Seek",
        OperatorDescription = "Index Seek",
        PlanNodeIdentifier = new PlanNodeIdentifier(2, 0)
    };

    private static readonly CallNode[] Nodes =
    [
        new(-1, GetRowFunction, RangeIterator, 1),
        new(0, SetRangeFunction, 0, 2),
        new(0, EvaluateFunction, 0, 2),
        new(2, GeneralEvalFunction, 0, 2),
        new(3, GetDataFunction, 0, 1),
        new(3, GetDataFromVariantFunction, 0, 2),
        new(0, GetDataFunction, 0, 1),
        new(0, RangeSetFunction, 0, 1),
        new(7, GeneralEvalFunction, 0, 1),
        new(8, GetDataFromVariantFunction, 0, 1)
    ];

    [Theory]
    [InlineData(5, 0)]
    [InlineData(20, 1)]
    [InlineData(31, 3)]
    [InlineData(50, 3)]
    public void The_Range_In_Scope_Is_The_Latest_One_Set(int position, int range)
    {
        var run = Create();

        Assert.Equal(range, TraceSeekRangeTrackBuilder.GetRange(run.Ranges, run.Calls, Seek, Thread, At((ulong)position)));
    }

    [Fact]
    public void A_Seek_That_Never_Sets_A_Range_Is_On_Its_Only_Range()
    {
        var run = Create();

        Assert.Equal(0, TraceSeekRangeTrackBuilder.GetRange(run.Ranges, run.Calls, SingleRangeSeek, Thread, At(50)));
    }

    [Fact]
    public void Inside_The_Key_Lookup_The_Key_Is_The_Value_Read_For_It()
        => Assert.Equal([5UL], GetKeys(20));

    [Fact]
    public void The_Key_Stays_In_Scope_Until_The_Next_One_Is_Read()
        => Assert.Equal([5UL], GetKeys(28));

    [Fact]
    public void A_Key_Read_When_The_Range_Is_Set_Applies_To_The_Seek_After_It()
        => Assert.Equal([8UL], GetKeys(58));

    [Fact]
    public void Before_Any_Key_Is_Read_There_Is_No_Key()
        => Assert.Empty(GetKeys(16));

    [Fact]
    public void A_Compound_Key_Has_One_Value_Per_Read_In_Key_Order()
        => Assert.Equal([6UL, 7UL], GetKeys(40));

    [Fact]
    public void Only_Values_Read_By_The_Range_Expression_Are_Keys()
        => Assert.Equal(4, Create().Keys.Count);

    private static List<ulong> GetKeys(int position)
    {
        var run = Create();

        return [.. TraceSeekRangeTrackBuilder.GetKeys(run.Keys, run.Calls, Seek, Thread, At((ulong)position)).Select(k => k.Value)];
    }

    private static SeekRun Create()
    {
        var builder = new TraceTimeline.Builder();

        builder.Add([Span(0, 10, 60),
                     Span(1, 12, 14),
                     Span(2, 15, 25),
                     Span(3, 16, 24),
                     Span(4, 17, 18),
                     Span(1, 30, 32, 1),
                     Span(2, 33, 45, 1),
                     Span(3, 34, 44, 1),
                     Span(5, 35, 36),
                     Span(5, 37, 38, 1),
                     Span(6, 50, 51, 1),
                     Span(7, 52, 56),
                     Span(8, 53, 55, 2),
                     Span(9, 54, 55, 2)]);

        var timeline = builder.Build(Nodes);

        var tree = new CallTree();

        var getRow = tree.AddCall(tree.Root, Frame(GetRowFunction, RangeIterator, "CQScanRangeNew", "GetRow"), 1);

        var setRange = tree.AddCall(getRow, Frame(SetRangeFunction, 0, "CRangeEnumeratorMult", "ExecSetRange"), 2);

        var evaluate = tree.AddCall(getRow, Frame(EvaluateFunction, 0, "CQScanRangeNew", "EvalSetRangeExpr"), 2);

        var generalEval = tree.AddCall(evaluate, Frame(GeneralEvalFunction, 0, "CEsExec", "GeneralEval4", "sqlTsEs"), 2);

        var getData = tree.AddCall(generalEval, Frame(GetDataFunction, 0, string.Empty, "GetData", "sqlTsEs"), 1);

        var variantFrame = Frame(GetDataFromVariantFunction, 0, string.Empty, "GetDataFromXvar8", "sqlTsEs");

        var getDataFromVariant = tree.AddCall(generalEval, variantFrame, 2);

        var otherGetData = tree.AddCall(getRow, Frame(GetDataFunction, 0, string.Empty, "GetData", "sqlTsEs"), 1);

        var rangeSet = tree.AddCall(getRow, Frame(RangeSetFunction, 0, "CQScanRangeNew", "SetRange"), 1);

        var rangeEval = tree.AddCall(rangeSet, Frame(GeneralEvalFunction, 0, "CEsExec", "GeneralEval4", "sqlTsEs"), 1);

        var rangeKey = tree.AddCall(rangeEval, variantFrame, 1);

        CallTreeNode[] nodes =
        [
            getRow, setRange, evaluate, generalEval, getData, getDataFromVariant, otherGetData, rangeSet, rangeEval, rangeKey
        ];

        timeline.MapNodes(nodes);

        var store = new TraceCallStore.Builder();

        store.Add(GetRowFunction, RangeIterator, Chunk(Call(10, 60, 0)), 1);

        store.Add(SetRangeFunction, 0, Chunk(Call(12, 14, 1, 1), Call(30, 32, 1, 0xDEAD_0000_0000_0003)), 2);

        store.Add(EvaluateFunction, 0, Chunk(Call(15, 25, 2), Call(33, 45, 2)), 2);

        store.Add(GeneralEvalFunction, 0, Chunk(Call(16, 24, 3), Call(34, 44, 3), Call(53, 55, 8)), 3);

        store.Add(RangeSetFunction, 0, Chunk(Call(52, 56, 7)), 1);

        store.Add(GetDataFunction, 0, Chunk(Call(17, 18, 4), Call(50, 51, 6)), 2);

        store.Add(GetDataFromVariantFunction, 0, Chunk(Call(35, 36, 5), Call(37, 38, 5), Call(54, 55, 9)), 3);

        store.AddLocator(GetDataFunction, 0, 0, VariantLocator.Create(KeyRegister, VariantStatus.Value, 5, 4), true);

        store.AddLocator(GetDataFromVariantFunction, 0, 0, VariantLocator.Create(KeyRegister, VariantStatus.Value, 6, 4), true);

        store.AddLocator(GetDataFromVariantFunction, 0, 1, VariantLocator.Create(KeyRegister, VariantStatus.Value, 7, 2), true);

        store.AddLocator(GetDataFromVariantFunction, 0, 2, VariantLocator.Create(KeyRegister, VariantStatus.Value, 8, 4), true);

        var calls = store.Build();

        calls.MapNodes(nodes);

        timeline.SetCalls(calls);

        var lifetime = new TraceOperatorLifetime(Thread, Seek, 0, [Whole()], MemoryUsageCurve.Empty, timeline.Scale)
        {
            Instances = [RangeIterator]
        };

        var lifetimes = new TraceOperatorLifetimeList([lifetime]);

        var enclosing = EnclosingOperatorCallIndex.Build(calls, lifetimes.OperatorsByInstance);

        return new SeekRun(TraceSeekRangeTrackBuilder.Build(timeline, calls, lifetimes, enclosing),
                           TraceSeekRangeTrackBuilder.BuildKeys(timeline, calls, lifetimes, enclosing),
                           calls);
    }

    private static ulong[] Call(ulong start, ulong end, int callNodeIndex, ulong range = 0)
        => Row(start, flags: Thread | TraceCallColumnSet.ReturnedFlag, r8: range, callNodeIndex: callNodeIndex, endSequence: end);

    private static CallFrame Frame(ulong address, ulong instance, string className, string method, string module = "sqlmin") => new()
    {
        Module = new(module),
        Rva = (uint)address,
        Address = address,
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

    private sealed record SeekRun(TraceTickSeries Ranges, TraceTickSeries Keys, TraceCallStore Calls);
}
