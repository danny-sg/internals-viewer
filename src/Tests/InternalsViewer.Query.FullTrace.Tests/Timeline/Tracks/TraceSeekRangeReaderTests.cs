using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
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

namespace InternalsViewer.Query.FullTrace.Tests.Timeline.Tracks;

[Trait("Category", "Unit")]
public class TraceSeekRangeReaderTests
{
    private const uint Thread = 7;

    private const ulong GetRowFunction = 0x1000;

    private const ulong SetRangeFunction = 0x2000;

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
        new(0, SetRangeFunction, 0, 2)
    ];

    [Theory]
    [InlineData(5, 0)]
    [InlineData(20, 1)]
    [InlineData(31, 3)]
    [InlineData(50, 3)]
    public void The_Range_In_Scope_Is_The_Latest_One_Set(int position, int range)
        => Assert.Equal(range, GetRange(Seek, position));

    [Fact]
    public void A_Seek_That_Never_Sets_A_Range_Is_On_Its_Only_Range()
        => Assert.Equal(0, GetRange(SingleRangeSeek, 50));

    private static int GetRange(ExecutionOperatorEvent operatorEvent, int position)
    {
        var builder = new TraceTimeline.Builder();

        builder.Add([Span(0, 10, 60), Span(1, 12, 14), Span(1, 30, 32, 1)]);

        var timeline = builder.Build(Nodes);

        var tree = new CallTree();

        var getRow = tree.AddCall(tree.Root, Frame(GetRowFunction, RangeIterator, "CQScanRangeNew", "GetRow"), 1);

        var setRange = tree.AddCall(getRow, Frame(SetRangeFunction, 0, "CRangeEnumeratorMult", "ExecSetRange"), 2);

        CallTreeNode[] nodes = [getRow, setRange];

        timeline.MapNodes(nodes);

        var store = new TraceCallStore.Builder();

        store.Add(GetRowFunction, RangeIterator, Chunk(Call(10, 60, 0, 0)), 1);

        store.Add(SetRangeFunction, 0, Chunk(Call(12, 14, 1, 1), Call(30, 32, 1, 0xDEAD_0000_0000_0003)), 2);

        var calls = store.Build();

        calls.MapNodes(nodes);

        timeline.SetCalls(calls);

        var lifetime = new TraceOperatorLifetime(Thread, Seek, 0, [Whole()], MemoryUsageCurve.Empty, timeline.Scale)
        {
            Instances = [RangeIterator]
        };

        var lifetimes = new TraceOperatorLifetimeList([lifetime]);

        var ranges = TraceSeekRangeReader.Build(timeline,
                                                calls,
                                                lifetimes,
                                                EnclosingOperatorCallIndex.Build(calls, lifetimes.OperatorsByInstance));

        return TraceSeekRangeReader.GetRange(ranges, calls, operatorEvent, Thread, At((ulong)position));
    }

    private static ulong[] Call(ulong start, ulong end, int callNodeIndex, ulong range)
        => Row(start, flags: Thread | TraceCallChunk.ReturnedFlag, r8: range, callNodeIndex: callNodeIndex, endSequence: end);

    private static CallFrame Frame(ulong address, ulong instance, string className, string method) => new()
    {
        Module = new("sqlmin"),
        Rva = 0x10,
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
}
