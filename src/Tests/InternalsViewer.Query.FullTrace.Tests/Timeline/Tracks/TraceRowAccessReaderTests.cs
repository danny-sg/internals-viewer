using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History;
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
public class TraceRowAccessReaderTests
{
    private const uint Thread = 7;

    private const ulong Helper = 0x1000;

    private const ulong GetFunction = 0x2000;

    private const ulong RecordFunction = 0x3000;

    private const ulong BufferPool = 0x1F4_0000_0100;

    private const ulong RangeIterator = 0x1F4_0000_1000;

    private const ulong BufferFrame = 0x20A_5448_0000;

    private const ulong Buffer = 0x14A_BB17_8340;

    private const ushort Database = 6;

    private static readonly PageAddress Page = new(1, 405);

    private static readonly ExecutionOperatorEvent Range = new()
    {
        Name = "Clustered Index Seek",
        OperatorDescription = "Clustered Index Seek",
        PlanNodeIdentifier = new PlanNodeIdentifier(1, 0)
    };

    private static readonly CallNode[] Nodes =
    [
        new(-1, Helper, RangeIterator, 1),
        new(0, GetFunction, BufferPool, 1),
        new(0, RecordFunction, 0, 1)
    ];

    [Fact]
    public void A_Row_Access_Is_A_Point_Where_The_Record_Was_Used()
    {
        var access = Assert.Single(Build().GetAllTicks());

        Assert.Equal((At(20), At(20), "Record::HasNullbitMap"), (access.Span.From.Position, access.Span.To.Position, access.Node.Symbol));
    }

    [Fact]
    public void A_Row_Access_Belongs_To_The_Operator_Call_Around_It() => Assert.Same(Range, Assert.Single(Build().GetAllTicks()).Operator);

    [Fact]
    public void A_Header_Access_Is_A_Point_Where_The_Header_Was_Read()
    {
        var access = Assert.Single(Build(new TracePageReadList([new TracePageRead(At(21), Thread, BufferFrame + 0x16, 2)])).GetAllTicks());

        Assert.Equal((At(21), At(21), "Record::HasNullbitMap", Range),
                     (access.Span.From.Position, access.Span.To.Position, access.Node.Symbol, access.Operator));
    }

    private static TraceTickSeries Build(TracePageReadList? headerReads = null)
    {
        var builder = new TraceTimeline.Builder();

        builder.Add([Span(0, 10, 60), Span(1, 12, 14), Span(2, 20, 22)]);

        var timeline = builder.Build(Nodes);

        var tree = new CallTree();

        var range = tree.AddCall(tree.Root, Frame(Helper, RangeIterator, "CQScanRangeNew", "GetRow"), 1);

        var get = tree.AddCall(range, Frame(GetFunction, BufferPool, "BPool", "Get"), 1);

        var record = tree.AddCall(range, Frame(RecordFunction, 0, "Record", "HasNullbitMap"), 1);

        CallTreeNode[] nodes = [range, get, record];

        timeline.MapNodes(nodes);

        var store = new TraceCallStore.Builder();

        store.Add(Helper, RangeIterator, Chunk(Call(10, 60, 0)), 1);

        store.Add(GetFunction, BufferPool, Chunk(Call(12, 14, 1, returnValue: Buffer)), 1);

        store.Add(RecordFunction, 0, Chunk(Call(20, 22, 2, rcx: BufferFrame + 0x5B3)), 1);

        var calls = store.Build();

        calls.MapNodes(nodes);

        timeline.SetCalls(calls);

        var pool = new TraceBufferPool.Builder();

        pool.Add(new TraceBufferPage(BufferFrame, Page, Database, At(1), Buffer));

        var accesses = TracePageAccessList.Build(calls, MemoryHistoryStore.Empty, pool.Build(), headerReads, timeline);

        var lifetime = new TraceOperatorLifetime(Thread, Range, 0, [Whole()], MemoryUsageCurve.Empty, timeline.Scale)
        {
            Instances = [RangeIterator]
        };

        var lifetimes = new TraceOperatorLifetimeList([lifetime]);

        var enclosing = EnclosingOperatorCallIndex.Build(calls, lifetimes.OperatorsByInstance);

        return headerReads is null
            ? TraceRowAccessReader.Build(timeline, accesses, lifetimes, enclosing)
            : TraceRowAccessReader.BuildHeaders(timeline, accesses, lifetimes, enclosing);
    }

    private static ulong[] Call(ulong start, ulong end, int callNodeIndex, ulong rcx = 0, ulong returnValue = 0)
        => Row(start,
               flags: Thread | TraceCallChunk.ReturnedFlag,
               rcx: rcx,
               returnValue: returnValue,
               callNodeIndex: callNodeIndex,
               endSequence: end);

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

    private static CallSpan Span(int callNodeIndex, ulong start, ulong end)
        => new(start, 0, end, 0, start, end, callNodeIndex, Thread, 0, 1);
}
