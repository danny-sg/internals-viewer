using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Plans.Model;
using static InternalsViewer.Query.FullTrace.Tests.TraceCallRows;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TraceReadsTests
{
    private const uint Thread = 7;

    private const ulong Helper = 0x1000;

    private const ulong GetFunction = 0x2000;

    private const ulong ReadAheadFunction = 0x3000;

    private const ulong GetFromDiskFunction = 0x4000;

    private const ulong BufferPool = 0x1F4_0000_0100;

    private const ulong JoinIterator = 0x1F4_0000_1000;

    private const ulong ScanIterator = 0x1F4_0000_2000;

    private static readonly ExecutionOperatorEvent Join = new()
    {
        Name = "Nested Loops",
        OperatorDescription = "Nested Loops",
        Instances = [JoinIterator],
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
        new(-1, 0, Helper, JoinIterator, 1),
        new(0, 0, Helper, ScanIterator, 1),
        new(1, 0, GetFunction, BufferPool, 1),
        new(0, 0, ReadAheadFunction, BufferPool, 1),
        new(-1, 0, GetFunction, BufferPool, 1),
        new(2, 0, GetFromDiskFunction, BufferPool, 1)
    ];

    [Fact]
    public void A_Buffer_Pool_Get_Is_A_One_Page_Read_Over_Its_Call()
    {
        var (reads, calls) = Build();

        var read = Assert.Single(reads.All, r => r.Value == (byte)TraceReadType.Page && r.Operator is not null);

        Assert.Equal(((new PageAddress(1, 1056), 1), 20d, 25d),
                     (GetRead(calls, read, TraceReadType.Page), read.Position.Start, read.Position.End));
    }

    [Fact]
    public void A_Read_Ahead_Reads_The_Page_Count_In_Its_Third_Argument()
    {
        var (reads, calls) = Build();

        var read = Assert.Single(reads.All, r => r.Value == (byte)TraceReadType.ReadAhead);

        Assert.Equal((new PageAddress(1, 1536), 40), GetRead(calls, read, TraceReadType.ReadAhead));
    }

    [Fact]
    public void A_Read_Belongs_To_The_Innermost_Operator_Call_Around_It()
    {
        var reads = Build().Reads;

        Assert.Same(Scan, reads.All.Single(r => r.Value == (byte)TraceReadType.Page && r.Operator is not null).Operator);

        Assert.Same(Join, reads.All.Single(r => r.Value == (byte)TraceReadType.ReadAhead).Operator);
    }

    [Fact]
    public void A_Read_Outside_Every_Operator_Call_Has_No_Operator()
    {
        var read = Assert.Single(Build().Reads.All, r => r.Position.Start == 70);

        Assert.Null(read.Operator);
    }

    [Fact]
    public void Buffer_Pool_Functions_Other_Than_Get_And_ReadAhead_Are_Not_Reads()
    {
        Assert.Equal(3, Build().Reads.Count);
    }

    [Fact]
    public void Reads_Are_Indexed_By_The_Operator_And_Thread_That_Made_Them()
    {
        var reads = Build().Reads;

        Assert.Equal((1, 1, null),
                     (reads.GetTicks(Scan, Thread)?.Count, reads.GetTicks(Join, Thread)?.Count, reads.GetTicks(Scan, Thread + 1)?.Count));
    }

    [Fact]
    public void The_Nearest_Read_Of_An_Operator_Within_The_Tolerance_Is_Found()
    {
        var reads = Build().Reads.GetTicks(Scan, Thread)!;

        Assert.Equal((20d, null),
                     (reads.GetNearest(TraceTimelineAxis.Position, 28, 5)?.Position.Start,
                      reads.GetNearest(TraceTimelineAxis.Position, 40, 5)?.Position.Start));
    }

    private static (TraceTickSeries Reads, TraceCallStore Calls) Build()
    {
        CallSpan[] spans =
        [
            Span(0, 10, 60),
            Span(1, 15, 30),
            Span(2, 20, 25),
            Span(3, 35, 45),
            Span(4, 70, 72, call: 1),
            Span(5, 21, 24)
        ];

        var builder = new TraceTimeline.Builder();

        builder.Add(spans);

        var timeline = builder.Build(Nodes);

        var tree = new CallStackTree();

        var join = tree.AddCall(tree.Root, Frame(JoinIterator, "CQScanNLJoinNew", "GetRow"), 1);

        var scan = tree.AddCall(join, Frame(ScanIterator, "CQScanTableScanNew", "GetRow"), 1);

        var scanGet = tree.AddCall(scan, Frame(BufferPool, "BPool", "Get"), 1);

        var readAhead = tree.AddCall(join, Frame(BufferPool, "BPool", "ReadAhead"), 1);

        var rootGet = tree.AddCall(tree.Root, Frame(BufferPool, "BPool", "Get"), 1);

        var fromDisk = tree.AddCall(scanGet, Frame(BufferPool, "BPool", "GetFromDisk"), 1);

        CallStackNode[] nodes = [join, scan, scanGet, readAhead, rootGet, fromDisk];

        timeline.MapNodes(nodes);

        var store = new TraceCallStore.Builder();

        store.Add(Helper, JoinIterator, Chunk(Call(10, 60, 0)), 1);

        store.Add(Helper, ScanIterator, Chunk(Call(15, 30, 1)), 1);

        store.Add(GetFunction, BufferPool, Chunk(Call(20, 25, 2), Call(70, 72, 4)), 2);

        store.Add(ReadAheadFunction, BufferPool, Chunk(Call(35, 45, 3, pageCount: 40)), 1);

        store.Add(GetFromDiskFunction, BufferPool, Chunk(Call(21, 24, 5, pageCount: 1)), 1);

        store.AddLocator(GetFunction, BufferPool, 0, Page(1, 1056), false);

        store.AddLocator(GetFunction, BufferPool, 1, Page(1, 1), false);

        store.AddLocator(ReadAheadFunction, BufferPool, 0, Page(1, 1536), false);

        store.AddLocator(GetFromDiskFunction, BufferPool, 0, Page(1, 1056), false);

        var calls = store.Build();

        calls.MapNodes(nodes);

        timeline.SetCalls(calls);

        timeline.SetLifetimes([new TraceOperatorLifetime(Thread, Join, 0, [Whole()], [Whole()], MemoryInUseCurve.Empty),
                               new TraceOperatorLifetime(Thread, Scan, 1, [Whole()], [Whole()], MemoryInUseCurve.Empty)]);

        return (TraceReads.Build(timeline, calls), calls);
    }

    private static (PageAddress Page, int PageCount)? GetRead(TraceCallStore calls, TraceTick read, TraceReadType type)
        => TraceReads.GetRead(calls.GetCallList(read.Function, read.Instance)![read.Index], type);

    private static ulong[] Call(ulong start, ulong end, int node, ulong pageCount = 0)
    {
        var row = Row(start, flags: Thread | TraceCallColumns.ReturnedFlag, rcx: BufferPool, node: node, endSequence: end);

        row[TraceCallColumns.IntegerOffset + 3] = pageCount;

        return row;
    }

    private static Locator Page(short file, int page) => new(2, LocatorValueType.Page, ((ulong)(ushort)file << 32) | (uint)page);

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
