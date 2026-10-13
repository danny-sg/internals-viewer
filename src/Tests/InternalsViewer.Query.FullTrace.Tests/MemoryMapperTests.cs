using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.FullTrace.CallLog;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Memory;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class MemoryMapperTests
{
    private const ulong Allocator = 0x5000;

    private const ulong Inner = 0x6000;

    private const ulong ReturnedFlag = 1ul << 32;

    private const string WorkspaceClerk = "MEMORYCLERK_SQLQERESERVATIONS";

    [Fact]
    public void Allocations_Are_Attributed_To_Their_Frame_And_Its_Callers()
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var allocator = tree.AddCall(caller, Frame(Allocator), 2);

        var builder = new TraceCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 100, node: 1), Row(2, size: 200, node: 1)), 2);

        var log = builder.Build();

        log.MapNodes([caller, allocator]);

        var (allocations, bytes) = MemoryMapper.Map(log, [Function(Allocator)], null);

        Assert.Equal((300ul, 2L), (bytes, allocations));
        Assert.Equal((300L, 2L), (allocator.AllocatedBytes, allocator.Allocations));
        Assert.Equal((300L, 2L), (caller.AllocatedBytesIncludingChildren, caller.AllocationsIncludingChildren));
        Assert.Equal(0L, caller.AllocatedBytes);
    }

    [Fact]
    public void An_Allocation_Made_Inside_Another_Is_Not_Counted_Twice()
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var outer = tree.AddCall(caller, Frame(Allocator), 1);
        var inner = tree.AddCall(outer, Frame(Inner), 1);

        var builder = new TraceCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 64, node: 1)), 1);
        builder.Add(Inner, 0, Chunk(Row(2, size: 8192, node: 2)), 1);

        var log = builder.Build();

        log.MapNodes([caller, outer, inner]);

        var (_, bytes) = MemoryMapper.Map(log, [Function(Allocator), Function(Inner)], null);

        Assert.Equal(64ul, bytes);
        Assert.Equal(64L, caller.AllocatedBytesIncludingChildren);
    }

    [Fact]
    public void Pages_Taken_By_A_Memory_Object_Are_Not_Counted_Again()
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var create = tree.AddCall(caller, Resolved(0x20, "sqldk", "MemoryObjectFactory", "CreateMemObject"), 1);
        var objectPages = tree.AddCall(create, Resolved(Inner, "sqldk", "MemoryClerkInternal", "AllocatePages"), 1);
        var workspacePages = tree.AddCall(caller, Resolved(Inner, "sqldk", "MemoryClerkInternal", "AllocatePages"), 1);

        var builder = new TraceCallLog.Builder();

        builder.Add(Inner, 0, Chunk(Row(1, size: 1, node: 2), Row(2, size: 2, node: 3)), 2);

        var log = builder.Build();

        log.MapNodes([caller, create, objectPages, workspacePages]);

        var (_, bytes) = MemoryMapper.Map(log, [ClerkPages()], null);

        Assert.Equal(16384ul, bytes);
    }

    [Theory]
    [InlineData("MEMORYCLERK_SQLQERESERVATIONS", 64ul)]
    [InlineData("MEMORYCLERK_SQLQUERYEXEC", 0ul)]
    public void Only_Memory_From_The_Workspace_Clerk_Counts_Against_The_Grant(string clerk, ulong expected)
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var allocator = tree.AddCall(caller, Frame(Allocator), 1);
        var pages = tree.AddCall(allocator, Resolved(Inner, "sqldk", "MemoryClerkInternal", "AllocatePages"), 1);

        var builder = new TraceCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 64, node: 1, returned: 0xA0)), 1);
        builder.Add(Inner, 0, Chunk(Row(2, size: 1, node: 2, receiver: 0xC1)), 1);

        var log = builder.Build();

        log.MapNodes([caller, allocator, pages]);

        var timelineBuilder = new TraceTimeline.Builder();

        timelineBuilder.Add([Span(0, 0, 40, uint.MaxValue), Span(1, 10, 20, 0), Span(2, 12, 13, 0)]);

        var timeline = timelineBuilder.Build([new(-1, 0, 0x10, 0, 1), new(0, 0, Allocator, 0, 1), new(1, 0, Inner, 0, 1)]);

        timeline.MapNodes([caller, allocator, pages]);

        MemoryMapper.Map(log, [Function(Allocator), ClerkPages()], timeline, Snapshot(clerks: new() { [0xC1] = clerk }));

        var start = timeline.StartOf(TraceTimelineAxis.Position);

        var end = timeline.EndOf(TraceTimelineAxis.Position) + 1;

        Assert.Equal((64ul, expected), (timeline.PeakInUseDuring(start, end), timeline.PeakWorkspaceDuring(start, end)));
    }

    [Fact]
    public void A_Memory_Object_Without_Pages_In_The_Recording_Takes_Its_Clerk_From_The_Snapshot()
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var allocator = tree.AddCall(caller, Resolved(Allocator, "sqldk", "CMemObj", "Alloc"), 1);

        var builder = new TraceCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 48, node: 1, returned: 0xA0, receiver: 0xB0)), 1);

        var log = builder.Build();

        log.MapNodes([caller, allocator]);

        var timelineBuilder = new TraceTimeline.Builder();

        timelineBuilder.Add([Span(0, 0, 40, uint.MaxValue), Span(1, 10, 20, 0)]);

        var timeline = timelineBuilder.Build([new(-1, 0, 0x10, 0, 1), new(0, 0, Allocator, 0, 1)]);

        timeline.MapNodes([caller, allocator]);

        MemoryMapper.Map(log,
                         [new(Allocator, MemoryOperation.Allocate, 1, -1, 1) { IsObjectCall = true }],
                         timeline,
                         Snapshot(objects: new() { [0xB0] = WorkspaceClerk }));

        Assert.Equal(48ul, PeakWorkspace(timeline));
    }

    [Fact]
    public void A_Memory_Object_Takes_Its_Clerk_From_The_Object_It_Allocates_From()
    {
        const ulong Parent = 0x8000;

        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var allocator = tree.AddCall(caller, Frame(Allocator), 1);
        var child = tree.AddCall(allocator, Resolved(Inner, "sqldk", "CMemThread<CMemObj>", "Alloc"), 1);
        var parent = tree.AddCall(child, Resolved(Parent, "sqldk", "CMemObj", "Alloc"), 1);

        var builder = new TraceCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 64, node: 1, returned: 0xA0)), 1);
        builder.Add(Inner, 0, Chunk(Row(2, size: 64, node: 2, receiver: 0xB1)), 1);
        builder.Add(Parent, 0, Chunk(Row(3, size: 64, node: 3, receiver: 0xB0)), 1);

        var log = builder.Build();

        log.MapNodes([caller, allocator, child, parent]);

        var timelineBuilder = new TraceTimeline.Builder();

        timelineBuilder.Add([Span(0, 0, 40, uint.MaxValue), Span(1, 10, 20, 0), Span(2, 11, 19, 0), Span(3, 12, 18, 0)]);

        var timeline = timelineBuilder.Build([new(-1, 0, 0x10, 0, 1),
                                              new(0, 0, Allocator, 0, 1),
                                              new(1, 0, Inner, 0, 1),
                                              new(2, 0, Parent, 0, 1)]);

        timeline.MapNodes([caller, allocator, child, parent]);

        var (_, bytes) = MemoryMapper.Map(log,
                                          [Function(Allocator),
                                           new(Inner, MemoryOperation.Allocate, 1, -1, 1) { IsObjectCall = true },
                                           new(Parent, MemoryOperation.Allocate, 1, -1, 1) { IsObjectCall = true }],
                                          timeline,
                                          Snapshot(objects: new() { [0xB0] = WorkspaceClerk }));

        Assert.Equal((64ul, 64ul), (PeakWorkspace(timeline), bytes));
    }

    [Fact]
    public void A_Later_Snapshot_Adds_To_An_Earlier_One()
    {
        var merged = Snapshot(clerks: new() { [0xC1] = "A", [0xC2] = "B" }).Merge(Snapshot(clerks: new() { [0xC2] = "C", [0xC3] = "D" }));

        Assert.Equal(["A", "C", "D"], [merged.Clerks[0xC1], merged.Clerks[0xC2], merged.Clerks[0xC3]]);
    }

    private static MemoryClerkSnapshot Snapshot(Dictionary<ulong, string>? clerks = null, Dictionary<ulong, string>? objects = null)
        => new(clerks ?? [], objects ?? []);

    private static MemoryFunction ClerkPages()
        => new(Inner, MemoryOperation.Allocate, 1, -1, 8192) { IsPage = true, CarriesClerk = true };

    private static CallSpan Span(int node, ulong start, ulong end, uint call)
        => new(1, start, 1, end, start, end, node, 7, call, 1);

    private static ulong PeakWorkspace(TraceTimeline timeline)
        => timeline.PeakWorkspaceDuring(timeline.StartOf(TraceTimelineAxis.Position), timeline.EndOf(TraceTimelineAxis.Position) + 1);

    private static CallstackFrame Resolved(ulong address,
                                           string module,
                                           string className,
                                           string methodName,
                                           SymbolCategory category = SymbolCategory.Unknown,
                                           string? iterator = null)
        => new()
        {
            Module = module,
            Rva = (uint)address,
            Address = address,
            Resolved = new ResolvedCallstackFrame
            {
                Module = module,
                ClassName = className,
                MethodName = methodName,
                SymbolCategory = category,
                Iterator = iterator
            }
        };

    private static MemoryFunction Function(ulong address) => new(address, MemoryOperation.Allocate, 1, -1, 1);

    private static CallstackFrame Frame(ulong address) => new() { Module = "sqldk", Rva = (uint)address, Address = address };

    private static ulong[] Row(ulong sequence, ulong size, int node, ulong returned = 0, ulong receiver = 0)
    {
        var row = new ulong[TraceArgumentCall.ValueCount];

        row[0] = sequence;
        row[1] = returned != 0 ? ReturnedFlag : 0;
        row[2] = receiver;
        row[3] = size;
        row[6] = returned;
        row[7] = (ulong)node;

        return row;
    }

    private static ulong[] Chunk(params ulong[][] rows)
    {
        var columns = new ulong[rows.Length * TraceArgumentCall.ValueCount];

        for (var call = 0; call < rows.Length; call++)
        {
            for (var value = 0; value < TraceArgumentCall.ValueCount; value++)
            {
                columns[value * rows.Length + call] = rows[call][value];
            }
        }

        return columns;
    }
}
