using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;
using InternalsViewer.Query.Plans.Model;
using static InternalsViewer.Query.FullTrace.Tests.TraceCallRowHelper;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;
using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Timeline.Tracks;

[Trait("Category", "Unit")]
public class TraceSeekReaderTests
{
    private const uint Thread = 7;

    private const ulong Helper = 0x1000;

    private const ulong GetPageWithKeyFunction = 0x2000;

    private const ulong GetFunction = 0x3000;

    private const ulong RowLookupFunction = 0x4000;

    private const ulong PageManager = 0x1F4_0000_0100;

    private const ulong BufferPool = 0x1F4_0000_0200;

    private const ulong RangeIterator = 0x1F4_0000_1000;

    private static readonly ExecutionOperatorEvent Range = new()
    {
        Name = "Clustered Index Seek",
        OperatorDescription = "Clustered Index Seek",
        PlanNodeIdentifier = new PlanNodeIdentifier(1, 0)
    };

    private static readonly CallNode[] Nodes =
    [
        new(-1, Helper, RangeIterator, 1),
        new(0, GetPageWithKeyFunction, PageManager, 1),
        new(1, GetFunction, BufferPool, 3),
        new(0, GetFunction, BufferPool, 1)
    ];

    [Fact]
    public void A_Seek_Is_One_Tick_Over_The_Whole_Descent()
    {
        var seek = Assert.Single(Build().GetAllTicks(), t => t.Value == (byte)TraceSeekMarkType.Seek);

        Assert.Equal((At(20), At(40)), (seek.Span.From.Position, seek.Span.To.Position));
    }

    [Fact]
    public void Each_Page_Fetched_In_The_Descent_Is_Marked_Where_Its_Fetch_Starts()
    {
        var descents = Build().GetAllTicks()
                              .Where(t => t.Value != (byte)TraceSeekMarkType.Seek)
                              .OrderBy(t => t.Span.From.Position)
                              .ToList();

        Assert.Equal([(TraceSeekMarkType.Root, At(22), At(22)),
                      (TraceSeekMarkType.Intermediate, At(26), At(26)),
                      (TraceSeekMarkType.Leaf, At(32), At(32))],
                     descents.Select(t => ((TraceSeekMarkType)t.Value, t.Span.From.Position, t.Span.To.Position)));
    }

    [Fact]
    public void Hovering_Next_To_A_Descent_Finds_The_Descent_Not_The_Seek_Around_It()
    {
        var ticks = Build().GetTicks(Range, Thread)!;

        var tick = ticks.GetNearest(TraceTimelineAxis.Position, ticks.Scale.GetValue(At(26)) + 1, 2);

        Assert.Equal((byte)TraceSeekMarkType.Intermediate, tick?.Value);
    }

    [Fact]
    public void Hovering_A_Seek_Away_From_Its_Descents_Finds_The_Seek()
    {
        var ticks = Build().GetTicks(Range, Thread)!;

        var tick = ticks.GetNearest(TraceTimelineAxis.Position, ticks.Scale.GetValue(At(36)), 1);

        Assert.Equal((byte)TraceSeekMarkType.Seek, tick?.Value);
    }

    [Fact]
    public void A_Seek_And_Its_Descents_Belong_To_The_Operator_Call_Around_Them()
        => Assert.All(Build().GetAllTicks(), t => Assert.Same(Range, t.Operator));

    [Fact]
    public void A_Seek_Covers_The_Row_Lookup_Around_Its_Descent()
    {
        var seek = Assert.Single(BuildRowLookup().GetAllTicks(), t => t.Value == (byte)TraceSeekMarkType.Seek);

        Assert.Equal((At(15), At(45)), (seek.Span.From.Position, seek.Span.To.Position));
    }

    [Fact]
    public void The_Pages_Of_A_Row_Lookup_Still_Come_From_The_Descent()
    {
        var levels = BuildRowLookup().GetAllTicks().Where(t => t.Value != (byte)TraceSeekMarkType.Seek).Select(t => t.Span.From.Position);

        Assert.Equal([At(22)], levels);
    }

    private static TraceTickSeries BuildRowLookup()
    {
        CallNode[] callNodes =
        [
            new(-1, Helper, RangeIterator, 1),
            new(0, RowLookupFunction, 0, 1),
            new(1, GetPageWithKeyFunction, PageManager, 1),
            new(2, GetFunction, BufferPool, 2)
        ];

        var builder = new TraceTimeline.Builder();

        builder.Add([Span(0, 10, 60), Span(1, 15, 45), Span(2, 20, 40), Span(3, 22, 23), Span(3, 42, 43, 1)]);

        var timeline = builder.Build(callNodes);

        var tree = new CallTree();

        var range = tree.AddCall(tree.Root, Frame(Helper, RangeIterator, "CQScanRangeNew", "GetRow"), 1);

        var lookup = tree.AddCall(range, Frame(RowLookupFunction, 0, string.Empty, "GetRowForKeyValue"), 1);

        var seek = tree.AddCall(lookup, Frame(GetPageWithKeyFunction, PageManager, "IndexPageManager", "GetPageWithKey"), 1);

        var get = tree.AddCall(seek, Frame(GetFunction, BufferPool, "BPool", "Get"), 2);

        CallTreeNode[] nodes = [range, lookup, seek, get];

        timeline.MapNodes(nodes);

        var store = new TraceCallStore.Builder();

        store.Add(Helper, RangeIterator, Chunk(Call(10, 60, 0)), 1);

        store.Add(RowLookupFunction, 0, Chunk(Call(15, 45, 1)), 1);

        store.Add(GetPageWithKeyFunction, PageManager, Chunk(Call(20, 40, 2)), 1);

        store.Add(GetFunction, BufferPool, Chunk(Call(22, 23, 3), Call(42, 43, 3)), 2);

        var calls = store.Build();

        calls.MapNodes(nodes);

        timeline.SetCalls(calls);

        var lifetime = new TraceOperatorLifetime(Thread, Range, 0, [Whole()], MemoryUsageCurve.Empty, timeline.Scale)
        {
            Instances = [RangeIterator]
        };

        var lifetimes = new TraceOperatorLifetimeList([lifetime]);

        return TraceSeekReader.Build(timeline, calls, lifetimes, EnclosingOperatorCallIndex.Build(calls, lifetimes.OperatorsByInstance));
    }

    private static TraceTickSeries Build()
    {
        var builder = new TraceTimeline.Builder();

        builder.Add([Span(0, 10, 60), Span(1, 20, 40), Span(2, 22, 23), Span(2, 26, 27, 1), Span(2, 32, 33, 2), Span(3, 50, 52, 3)]);

        var timeline = builder.Build(Nodes);

        var tree = new CallTree();

        var range = tree.AddCall(tree.Root, Frame(Helper, RangeIterator, "CQScanRangeNew", "GetRow"), 1);

        var seek = tree.AddCall(range, Frame(GetPageWithKeyFunction, PageManager, "IndexPageManager", "GetPageWithKey"), 1);

        var seekGet = tree.AddCall(seek, Frame(GetFunction, BufferPool, "BPool", "Get"), 3);

        var rangeGet = tree.AddCall(range, Frame(GetFunction, BufferPool, "BPool", "Get"), 1);

        CallTreeNode[] nodes = [range, seek, seekGet, rangeGet];

        timeline.MapNodes(nodes);

        var store = new TraceCallStore.Builder();

        store.Add(Helper, RangeIterator, Chunk(Call(10, 60, 0)), 1);

        store.Add(GetPageWithKeyFunction, PageManager, Chunk(Call(20, 40, 1)), 1);

        store.Add(GetFunction, BufferPool, Chunk(Call(22, 23, 2), Call(26, 27, 2), Call(32, 33, 2), Call(50, 52, 3)), 4);

        var calls = store.Build();

        calls.MapNodes(nodes);

        timeline.SetCalls(calls);

        var lifetime = new TraceOperatorLifetime(Thread, Range, 0, [Whole()], MemoryUsageCurve.Empty, timeline.Scale)
        {
            Instances = [RangeIterator]
        };

        var lifetimes = new TraceOperatorLifetimeList([lifetime]);

        return TraceSeekReader.Build(timeline, calls, lifetimes, EnclosingOperatorCallIndex.Build(calls, lifetimes.OperatorsByInstance));
    }

    private static ulong[] Call(ulong start, ulong end, int callNodeIndex)
        => Row(start, flags: Thread | TraceCallChunk.ReturnedFlag, callNodeIndex: callNodeIndex, endSequence: end);

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
