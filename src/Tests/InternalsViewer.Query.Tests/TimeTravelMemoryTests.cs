using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Memory;
using InternalsViewer.Query.CallStack.TimeTravel.Native;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class TimeTravelMemoryTests
{
    private const ulong Allocator = 0x5000;

    private const ulong Inner = 0x6000;

    private const ulong Releaser = 0x7000;

    private const ulong ReturnedFlag = 1ul << 32;

    [Fact]
    public void Allocations_Are_Attributed_To_Their_Frame_And_Its_Callers()
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var allocator = tree.AddCall(caller, Frame(Allocator), 2);

        var builder = new TimeTravelCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 100, node: 1), Row(2, size: 200, node: 1)), 2);

        var log = builder.Build();

        log.MapNodes([caller, allocator]);

        var summary = TimeTravelMemory.Apply(log, [Function(Allocator)], null, []);

        Assert.Equal((300ul, 2L), (summary.Bytes, summary.Allocations));
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

        var builder = new TimeTravelCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 64, node: 1)), 1);
        builder.Add(Inner, 0, Chunk(Row(2, size: 8192, node: 2)), 1);

        var log = builder.Build();

        log.MapNodes([caller, outer, inner]);

        var summary = TimeTravelMemory.Apply(log, [Function(Allocator), Function(Inner)], null, []);

        Assert.Equal(64ul, summary.Bytes);
        Assert.Equal(64L, caller.AllocatedBytesIncludingChildren);
    }

    [Fact]
    public void A_Free_Is_Matched_By_The_Pointer_An_Allocation_Returned()
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var allocator = tree.AddCall(caller, Frame(Allocator), 1);
        var free = tree.AddCall(caller, Frame(Releaser), 2);

        var builder = new TimeTravelCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 64, node: 1, returned: 0xABC0)), 1);
        builder.Add(Releaser, 0, Chunk(Row(2, size: 0xABC0, node: 2), Row(3, size: 0xDEF0, node: 2)), 2);

        var log = builder.Build();

        log.MapNodes([caller, allocator, free]);

        var summary = TimeTravelMemory.Apply(log, [Function(Allocator), new(Releaser, MemoryOperation.Free, -1, 1, 1)], null, []);

        var kind = Assert.Single(summary.Kinds);

        Assert.Equal((64ul, 64ul, 0ul), (kind.Allocated, kind.Freed, kind.Held));
    }

    [Fact]
    public void Uses_Are_Grouped_By_Caller_And_Allocator()
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var allocator = tree.AddCall(caller, Frame(Allocator), 2);

        var builder = new TimeTravelCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 100, node: 1), Row(2, size: 50, node: 1)), 2);

        var log = builder.Build();

        log.MapNodes([caller, allocator]);

        var kind = Assert.Single(TimeTravelMemory.Apply(log, [Function(Allocator)], null, []).Kinds);

        var use = Assert.Single(kind.Uses);

        Assert.Equal((150ul, 2L), (use.Bytes, use.Allocations));
    }

    [Fact]
    public void Memory_Still_In_Use_Is_What_Was_Allocated_And_Not_Freed()
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var allocator = tree.AddCall(caller, Frame(Allocator), 2);
        var free = tree.AddCall(caller, Frame(Releaser), 1);

        var builder = new TimeTravelCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 64, node: 1, returned: 0xA0), Row(2, size: 32, node: 1, returned: 0xB0)), 2);
        builder.Add(Releaser, 0, Chunk(Row(3, size: 0xA0, node: 2)), 1);

        var log = builder.Build();

        log.MapNodes([caller, allocator, free]);

        var summary = TimeTravelMemory.Apply(log, [Function(Allocator), new(Releaser, MemoryOperation.Free, -1, 1, 1)], null, []);

        var kind = Assert.Single(summary.Kinds);

        Assert.Equal((96ul, 64ul, 96ul, 32ul), (kind.Allocated, kind.Freed, kind.PeakInUse, kind.Held));
        Assert.Equal(96ul, summary.PeakInUse);
    }

    [Fact]
    public void Pages_Stolen_From_The_Buffer_Pool_Are_Used_By_The_Caller_Of_The_Steal()
    {
        var tree = new CallStackTree();

        var build = tree.AddCall(tree.Root, Resolved(0x10, "sqlmin", "CQScanHash", "ConsumeBuild", iterator: "Hash Match Build"), 1);
        var workfile = tree.AddCall(build, Resolved(0x20, "sqlmin", "WORKFILE", "AllocRowBufFromCrntBuf"), 1);
        var steal = tree.AddCall(workfile, Resolved(0x30, "sqlmin", "BPool", "Steal", SymbolCategory.BufferPool), 1);
        var block = tree.AddCall(steal, Resolved(0x40, "sqldk", "SOS_MemoryBlockAllocator", "AllocateBlock"), 1);
        var pages = tree.AddCall(block, Resolved(Allocator, "sqldk", "MemoryClerkInternal", "AllocateReservedPages"), 1);

        var builder = new TimeTravelCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 1, node: 4)), 1);

        var log = builder.Build();

        log.MapNodes([build, workfile, steal, block, pages]);

        var kind = Assert.Single(TimeTravelMemory.Apply(log, [new(Allocator, MemoryOperation.Allocate, 1, -1, 8192)], null, [])
                                     .Kinds);

        var use = Assert.Single(kind.Uses);

        Assert.Equal(("WORKFILE::AllocRowBufFromCrntBuf", "BPool::Steal", 8192ul), (use.Caller, use.Allocator, use.Bytes));
    }

    [Fact]
    public void Memory_Belongs_To_The_Innermost_Operator_And_Its_Statement()
    {
        var tree = new CallStackTree();

        var query = tree.AddCall(tree.Root, Frame(0x10), 1);
        var join = tree.AddCall(query, Frame(0x20), 1);
        var scan = tree.AddCall(join, Frame(0x30), 1);
        var joinAllocator = tree.AddCall(join, Frame(Allocator), 1);
        var scanAllocator = tree.AddCall(scan, Frame(Allocator), 1);
        var free = tree.AddCall(scan, Frame(Releaser), 1);

        var builder = new TimeTravelCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 100, node: 3, returned: 0xA0), Row(2, size: 40, node: 4, returned: 0xB0)), 2);
        builder.Add(Releaser, 0, Chunk(Row(3, size: 0xB0, node: 5)), 1);

        var log = builder.Build();

        log.MapNodes([query, join, scan, joinAllocator, scanAllocator, free]);

        var statement = Operator(-1, query);
        var hashMatch = Operator(1, join);
        var tableScan = Operator(2, scan);
        var unmatched = Operator(3);

        var summary = TimeTravelMemory.Apply(log,
                                             [Function(Allocator), new(Releaser, MemoryOperation.Free, -1, 1, 1)],
                                             null,
                                             [statement, hashMatch, tableScan, unmatched]);

        Assert.Equal((100ul, 0ul), (hashMatch.Memory!.Allocated, hashMatch.Memory.Freed));
        Assert.Equal((40ul, 40ul, 0ul), (tableScan.Memory!.Allocated, tableScan.Memory.Freed, tableScan.Memory.Held));
        Assert.Equal((140ul, 40ul, 140ul), (statement.Memory!.Allocated, statement.Memory.Freed, statement.Memory.PeakInUse));
        Assert.Null(unmatched.Memory);
    }

    [Fact]
    public void Memory_Under_A_Shared_Entry_Frame_Is_Not_Guessed()
    {
        var tree = new CallStackTree();

        var scan = tree.AddCall(tree.Root, Frame(0x10), 1);
        var allocator = tree.AddCall(scan, Frame(Allocator), 1);

        var builder = new TimeTravelCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 100, node: 1)), 1);

        var log = builder.Build();

        log.MapNodes([scan, allocator]);

        var outer = Operator(1, scan);
        var inner = Operator(2, scan);

        TimeTravelMemory.Apply(log, [Function(Allocator)], null, [outer, inner]);

        Assert.Equal(((TimeTravelMemoryPurpose?)null, (TimeTravelMemoryPurpose?)null), (outer.Memory, inner.Memory));
    }

    [Fact]
    public void Pages_Taken_By_A_Memory_Object_Are_Not_Counted_Again()
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var create = tree.AddCall(caller, Resolved(0x20, "sqldk", "MemoryObjectFactory", "CreateMemObject"), 1);
        var objectPages = tree.AddCall(create, Resolved(Inner, "sqldk", "MemoryClerkInternal", "AllocatePages"), 1);
        var workspacePages = tree.AddCall(caller, Resolved(Inner, "sqldk", "MemoryClerkInternal", "AllocatePages"), 1);

        var builder = new TimeTravelCallLog.Builder();

        builder.Add(Inner, 0, Chunk(Row(1, size: 1, node: 2), Row(2, size: 2, node: 3)), 2);

        var log = builder.Build();

        log.MapNodes([caller, create, objectPages, workspacePages]);

        var summary = TimeTravelMemory.Apply(log, [ClerkPages()], null, []);

        Assert.Equal(16384ul, summary.Bytes);
    }

    [Fact]
    public void Memory_Belongs_To_The_Clerk_Whose_Pages_It_Took()
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var allocator = tree.AddCall(caller, Frame(Allocator), 1);
        var pages = tree.AddCall(allocator, Resolved(Inner, "sqldk", "MemoryClerkInternal", "AllocatePages"), 1);

        var builder = new TimeTravelCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 64, node: 1, returned: 0xA0)), 1);
        builder.Add(Inner, 0, Chunk(Row(2, size: 1, node: 2, receiver: 0xC1)), 1);

        var log = builder.Build();

        log.MapNodes([caller, allocator, pages]);

        var timelineBuilder = new TimeTravelTimeline.Builder();

        timelineBuilder.Add([Span(0, 0, 40, uint.MaxValue), Span(1, 10, 20, 0), Span(2, 12, 13, 0)]);

        var timeline = timelineBuilder.Build([new(-1, 0, 0x10, 0, 1), new(0, 0, Allocator, 0, 1), new(1, 0, Inner, 0, 1)]);

        timeline.MapNodes([caller, allocator, pages]);

        var summary = TimeTravelMemory.Apply(log,
                                             [Function(Allocator), ClerkPages()],
                                             timeline,
                                             [],
                                             Snapshot(clerks: new() { [0xC1] = "MEMORYCLERK_SQLQUERYEXEC" }));

        var kind = Assert.Single(summary.Kinds);

        Assert.Equal(("MEMORYCLERK_SQLQUERYEXEC", 64ul), (kind.Name, kind.Allocated));
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

        var builder = new TimeTravelCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 64, node: 1, returned: 0xA0)), 1);
        builder.Add(Inner, 0, Chunk(Row(2, size: 1, node: 2, receiver: 0xC1)), 1);

        var log = builder.Build();

        log.MapNodes([caller, allocator, pages]);

        var timelineBuilder = new TimeTravelTimeline.Builder();

        timelineBuilder.Add([Span(0, 0, 40, uint.MaxValue), Span(1, 10, 20, 0), Span(2, 12, 13, 0)]);

        var timeline = timelineBuilder.Build([new(-1, 0, 0x10, 0, 1), new(0, 0, Allocator, 0, 1), new(1, 0, Inner, 0, 1)]);

        timeline.MapNodes([caller, allocator, pages]);

        TimeTravelMemory.Apply(log, [Function(Allocator), ClerkPages()], timeline, [], Snapshot(clerks: new() { [0xC1] = clerk }));

        var start = timeline.StartOf(TimeTravelTimelineAxis.Position);

        var end = timeline.EndOf(TimeTravelTimelineAxis.Position) + 1;

        Assert.Equal((64ul, expected), (timeline.PeakInUseDuring(start, end), timeline.PeakWorkspaceDuring(start, end)));
    }

    [Fact]
    public void A_Memory_Object_Without_Pages_In_The_Recording_Takes_Its_Clerk_From_The_Snapshot()
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var allocator = tree.AddCall(caller, Resolved(Allocator, "sqldk", "CMemObj", "Alloc"), 1);

        var builder = new TimeTravelCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 48, node: 1, returned: 0xA0, receiver: 0xB0)), 1);

        var log = builder.Build();

        log.MapNodes([caller, allocator]);

        var timelineBuilder = new TimeTravelTimeline.Builder();

        timelineBuilder.Add([Span(0, 0, 40, uint.MaxValue), Span(1, 10, 20, 0)]);

        var timeline = timelineBuilder.Build([new(-1, 0, 0x10, 0, 1), new(0, 0, Allocator, 0, 1)]);

        timeline.MapNodes([caller, allocator]);

        var summary = TimeTravelMemory.Apply(log,
                                             [new(Allocator, MemoryOperation.Allocate, 1, -1, 1) { IsObjectCall = true }],
                                             timeline,
                                             [],
                                             Snapshot(objects: new() { [0xB0] = "MEMORYCLERK_SQLGENERAL" }));

        Assert.Equal("MEMORYCLERK_SQLGENERAL", Assert.Single(summary.Kinds).Name);
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

        var builder = new TimeTravelCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 64, node: 1, returned: 0xA0)), 1);
        builder.Add(Inner, 0, Chunk(Row(2, size: 64, node: 2, receiver: 0xB1)), 1);
        builder.Add(Parent, 0, Chunk(Row(3, size: 64, node: 3, receiver: 0xB0)), 1);

        var log = builder.Build();

        log.MapNodes([caller, allocator, child, parent]);

        var timelineBuilder = new TimeTravelTimeline.Builder();

        timelineBuilder.Add([Span(0, 0, 40, uint.MaxValue), Span(1, 10, 20, 0), Span(2, 11, 19, 0), Span(3, 12, 18, 0)]);

        var timeline = timelineBuilder.Build([new(-1, 0, 0x10, 0, 1),
                                              new(0, 0, Allocator, 0, 1),
                                              new(1, 0, Inner, 0, 1),
                                              new(2, 0, Parent, 0, 1)]);

        timeline.MapNodes([caller, allocator, child, parent]);

        var summary = TimeTravelMemory.Apply(log,
                                             [Function(Allocator),
                                              new(Inner, MemoryOperation.Allocate, 1, -1, 1) { IsObjectCall = true },
                                              new(Parent, MemoryOperation.Allocate, 1, -1, 1) { IsObjectCall = true }],
                                             timeline,
                                             [],
                                             Snapshot(objects: new() { [0xB0] = "MEMORYCLERK_SQLGENERAL" }));

        Assert.Equal(("MEMORYCLERK_SQLGENERAL", 64ul), (Assert.Single(summary.Kinds).Name, summary.Bytes));
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

    private static TimeTravelCallSpan Span(int node, ulong start, ulong end, uint call)
        => new(1, start, 1, end, start, end, node, 7, call, 1);

    private static ExecutionOperatorEvent Operator(int nodeId, params CallStackNode[] entryFrames) => new()
    {
        Name = "Operator",
        OperatorDescription = "Operator",
        PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 1, NodeId = nodeId },
        EntryFrames = entryFrames
    };

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
        var row = new ulong[TimeTravelArgumentCall.ValueCount];

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
        var columns = new ulong[rows.Length * TimeTravelArgumentCall.ValueCount];

        for (var call = 0; call < rows.Length; call++)
        {
            for (var value = 0; value < TimeTravelArgumentCall.ValueCount; value++)
            {
                columns[value * rows.Length + call] = rows[call][value];
            }
        }

        return columns;
    }
}
