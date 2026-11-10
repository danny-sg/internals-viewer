using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Allocation.Enums;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Memory;
using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.Allocation;

[Trait("Category", "Unit")]
public class AllocationMapperTests
{
    private const ulong Allocator = 0x5000;

    private const ulong Inner = 0x6000;

    private const ulong ReturnedFlag = TraceCallChunk.ReturnedFlag;

    private const string WorkspaceClerk = "MEMORYCLERK_SQLQERESERVATIONS";

    [Fact]
    public void Allocations_Are_Attributed_To_Their_Frame_And_Its_Callers()
    {
        var tree = new CallTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var allocator = tree.AddCall(caller, Frame(Allocator), 2);

        var builder = new TraceCallStore.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 100, callNodeIndex: 1), Row(2, size: 200, callNodeIndex: 1)), 2);

        var store = builder.Build();

        store.MapNodes([caller, allocator]);

        var (_, allocations, bytes, _) = AllocationMapper.Map(store, [Function(Allocator)], null);

        Assert.Equal((300ul, 2L), (bytes, allocations));
        Assert.Equal((300L, 2L), (allocator.AllocatedBytes, allocator.AllocationCount));
        Assert.Equal((300L, 2L), (caller.AllocatedBytesIncludingChildren, caller.AllocationsIncludingChildren));
        Assert.Equal(0L, caller.AllocatedBytes);
    }

    [Fact]
    public void An_Allocation_Made_Inside_Another_Is_Not_Counted_Twice()
    {
        var tree = new CallTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var outer = tree.AddCall(caller, Frame(Allocator), 1);
        var inner = tree.AddCall(outer, Frame(Inner), 1);

        var builder = new TraceCallStore.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 64, callNodeIndex: 1)), 1);
        builder.Add(Inner, 0, Chunk(Row(2, size: 8192, callNodeIndex: 2)), 1);

        var store = builder.Build();

        store.MapNodes([caller, outer, inner]);

        var (_, _, bytes, _) = AllocationMapper.Map(store, [Function(Allocator), Function(Inner)], null);

        Assert.Equal(64ul, bytes);
        Assert.Equal(64L, caller.AllocatedBytesIncludingChildren);
    }

    [Fact]
    public void Pages_Taken_By_A_Memory_Object_Are_Not_Counted_Again()
    {
        var tree = new CallTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var create = tree.AddCall(caller, Resolved(0x20, "sqldk", "MemoryObjectFactory", "CreateMemObject"), 1);
        var objectPages = tree.AddCall(create, Resolved(Inner, "sqldk", "MemoryClerkInternal", "AllocatePages"), 1);
        var workspacePages = tree.AddCall(caller, Resolved(Inner, "sqldk", "MemoryClerkInternal", "AllocatePages"), 1);

        var builder = new TraceCallStore.Builder();

        builder.Add(Inner, 0, Chunk(Row(1, size: 1, callNodeIndex: 2), Row(2, size: 2, callNodeIndex: 3)), 2);

        var store = builder.Build();

        store.MapNodes([caller, create, objectPages, workspacePages]);

        var (_, _, bytes, _) = AllocationMapper.Map(store, [ClerkPages()], null);

        Assert.Equal(16384ul, bytes);
    }

    [Theory]
    [InlineData("MEMORYCLERK_SQLQERESERVATIONS", 64ul)]
    [InlineData("MEMORYCLERK_SQLQUERYEXEC", 0ul)]
    public void Only_Memory_From_The_Workspace_Clerk_Counts_Against_The_Grant(string clerk, ulong expected)
    {
        var tree = new CallTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var allocator = tree.AddCall(caller, Frame(Allocator), 1);
        var pages = tree.AddCall(allocator, Resolved(Inner, "sqldk", "MemoryClerkInternal", "AllocatePages"), 1);

        var builder = new TraceCallStore.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 64, callNodeIndex: 1, returned: 0xA0)), 1);
        builder.Add(Inner, 0, Chunk(Row(2, size: 1, callNodeIndex: 2, receiver: 0xC1)), 1);

        var store = builder.Build();

        store.MapNodes([caller, allocator, pages]);

        var timelineBuilder = new TraceTimeline.Builder();

        timelineBuilder.Add([Span(0, 0, 40, uint.MaxValue), Span(1, 10, 20, 0), Span(2, 12, 13, 0)]);

        var timeline = timelineBuilder.Build([new(-1, 0x10, 0, 1), new(0, Allocator, 0, 1), new(1, Inner, 0, 1)]);

        timeline.MapNodes([caller, allocator, pages]);

        var (memory, _, _, _) = AllocationMapper.Map(store,
                                                     [Function(Allocator), ClerkPages()],
                                                     timeline,
                                                     Snapshot(clerks: new() { [0xC1] = clerk }));

        var start = timeline.Start.Position;

        var end = timeline.End.Position.Next;

        Assert.Equal((64ul, expected), (memory.GetPeakInUseDuring(start, end), memory.GetPeakWorkspaceDuring(start, end)));
    }

    [Fact]
    public void A_Memory_Object_Without_Pages_In_The_Recording_Takes_Its_Clerk_From_The_Snapshot()
    {
        var tree = new CallTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var allocator = tree.AddCall(caller, Resolved(Allocator, "sqldk", "CMemObj", "Alloc"), 1);

        var builder = new TraceCallStore.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 48, callNodeIndex: 1, returned: 0xA0, receiver: 0xB0)), 1);

        var store = builder.Build();

        store.MapNodes([caller, allocator]);

        var timelineBuilder = new TraceTimeline.Builder();

        timelineBuilder.Add([Span(0, 0, 40, uint.MaxValue), Span(1, 10, 20, 0)]);

        var timeline = timelineBuilder.Build([new(-1, 0x10, 0, 1), new(0, Allocator, 0, 1)]);

        timeline.MapNodes([caller, allocator]);

        var (memory, _, _, _) = AllocationMapper.Map(store,
                                                     [new(Allocator, AllocationOperation.Allocate, 1, -1, 1) { IsObjectCall = true }],
                                                     timeline,
                                                     Snapshot(objectClerks: new() { [0xB0] = WorkspaceClerk }));

        Assert.Equal(48ul, PeakWorkspace(timeline, memory));
    }

    [Fact]
    public void A_Memory_Object_Takes_Its_Clerk_From_The_Object_It_Allocates_From()
    {
        const ulong Parent = 0x8000;

        var tree = new CallTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var allocator = tree.AddCall(caller, Frame(Allocator), 1);
        var child = tree.AddCall(allocator, Resolved(Inner, "sqldk", "CMemThread<CMemObj>", "Alloc"), 1);
        var parent = tree.AddCall(child, Resolved(Parent, "sqldk", "CMemObj", "Alloc"), 1);

        var builder = new TraceCallStore.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 64, callNodeIndex: 1, returned: 0xA0)), 1);
        builder.Add(Inner, 0, Chunk(Row(2, size: 64, callNodeIndex: 2, receiver: 0xB1)), 1);
        builder.Add(Parent, 0, Chunk(Row(3, size: 64, callNodeIndex: 3, receiver: 0xB0)), 1);

        var store = builder.Build();

        store.MapNodes([caller, allocator, child, parent]);

        var timelineBuilder = new TraceTimeline.Builder();

        timelineBuilder.Add([Span(0, 0, 40, uint.MaxValue), Span(1, 10, 20, 0), Span(2, 11, 19, 0), Span(3, 12, 18, 0)]);

        var timeline = timelineBuilder.Build([new(-1, 0x10, 0, 1),
                                              new(0, Allocator, 0, 1),
                                              new(1, Inner, 0, 1),
                                              new(2, Parent, 0, 1)]);

        timeline.MapNodes([caller, allocator, child, parent]);

        var (memory, _, bytes, _) = AllocationMapper.Map(store,
                                                         [Function(Allocator),
                                                          new(Inner, AllocationOperation.Allocate, 1, -1, 1) { IsObjectCall = true },
                                                          new(Parent, AllocationOperation.Allocate, 1, -1, 1) { IsObjectCall = true }],
                                                         timeline,
                                                         Snapshot(objectClerks: new() { [0xB0] = WorkspaceClerk }));

        Assert.Equal((64ul, 64ul), (PeakWorkspace(timeline, memory), bytes));
    }

    [Fact]
    public void An_Allocation_Takes_The_Type_Of_The_Memory_Object_It_Came_From()
    {
        var tree = new CallTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var allocator = tree.AddCall(caller, Frame(Allocator), 1);
        var memoryObject = tree.AddCall(allocator, Resolved(Inner, "sqldk", "CMemProc", "Alloc"), 1);

        var builder = new TraceCallStore.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 888, callNodeIndex: 1, returned: 0xA0)), 1);
        builder.Add(Inner, 0, Chunk(Row(2, size: 888, callNodeIndex: 2, returned: 0xA0, receiver: 0xB0)), 1);

        var store = builder.Build();

        store.MapNodes([caller, allocator, memoryObject]);

        var timelineBuilder = new TraceTimeline.Builder();

        timelineBuilder.Add([Span(0, 0, 40, uint.MaxValue), Span(1, 10, 20, 0), Span(2, 11, 19, 0)]);

        var timeline = timelineBuilder.Build([new(-1, 0x10, 0, 1), new(0, Allocator, 0, 1), new(1, Inner, 0, 1)]);

        timeline.MapNodes([caller, allocator, memoryObject]);

        var (_, _, _, memoryObjects) = AllocationMapper.Map(store,
                                                            [Function(Allocator),
                                                             new(Inner, AllocationOperation.Allocate, 1, -1, 1) { IsObjectCall = true }],
                                                            timeline,
                                                            Snapshot(objectTypes: new() { [0xB0] = "MEMOBJ_EXECUTE" }));

        Assert.Equal(("MEMOBJ_EXECUTE", "MEMOBJ_EXECUTE"),
                     (memoryObjects.GetMemoryObject(Allocator, 0), memoryObjects.GetMemoryObject(Inner, 0)));
    }

    [Theory]
    [InlineData(0xB0ul, "MEMOBJ_SORT")]
    [InlineData(0xB1ul, "MEMOBJ_SORT")]
    [InlineData(0xB2ul, null)]
    public void A_Memory_Object_Missing_From_The_Snapshot_Takes_The_Type_Of_The_One_It_Allocates_From(ulong listed, string? expected)
    {
        const ulong Parent = 0x8000;

        var tree = new CallTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var allocator = tree.AddCall(caller, Frame(Allocator), 1);
        var child = tree.AddCall(allocator, Resolved(Inner, "sqldk", "CMemThread<CMemObj>", "Alloc"), 1);
        var parent = tree.AddCall(child, Resolved(Parent, "sqldk", "CMemObj", "Alloc"), 1);

        var builder = new TraceCallStore.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 64, callNodeIndex: 1, returned: 0xA0)), 1);
        builder.Add(Inner, 0, Chunk(Row(2, size: 64, callNodeIndex: 2, receiver: 0xB1)), 1);
        builder.Add(Parent, 0, Chunk(Row(3, size: 64, callNodeIndex: 3, receiver: 0xB0)), 1);

        var store = builder.Build();

        store.MapNodes([caller, allocator, child, parent]);

        var timelineBuilder = new TraceTimeline.Builder();

        timelineBuilder.Add([Span(0, 0, 40, uint.MaxValue), Span(1, 10, 20, 0), Span(2, 11, 19, 0), Span(3, 12, 18, 0)]);

        var timeline = timelineBuilder.Build([new(-1, 0x10, 0, 1),
                                              new(0, Allocator, 0, 1),
                                              new(1, Inner, 0, 1),
                                              new(2, Parent, 0, 1)]);

        timeline.MapNodes([caller, allocator, child, parent]);

        var (_, _, _, memoryObjects) = AllocationMapper.Map(store,
                                                            [Function(Allocator),
                                                             new(Inner, AllocationOperation.Allocate, 1, -1, 1) { IsObjectCall = true },
                                                             new(Parent, AllocationOperation.Allocate, 1, -1, 1) { IsObjectCall = true }],
                                                            timeline,
                                                            Snapshot(objectTypes: new() { [listed] = "MEMOBJ_SORT" }));

        Assert.Equal(expected, memoryObjects.GetMemoryObject(Allocator, 0));
    }

    [Fact]
    public void A_Later_Snapshot_Adds_To_An_Earlier_One()
    {
        var merged = Snapshot(clerks: new() { [0xC1] = "A", [0xC2] = "B" }).Merge(Snapshot(clerks: new() { [0xC2] = "C", [0xC3] = "D" }));

        Assert.Equal(["A", "C", "D"], [merged.Clerks[0xC1], merged.Clerks[0xC2], merged.Clerks[0xC3]]);
    }

    [Fact]
    public void A_Later_Snapshot_Adds_Memory_Object_Types_To_An_Earlier_One()
    {
        var earlier = Snapshot(objectTypes: new() { [0xB0] = "MEMOBJ_EXECUTE" });

        var merged = earlier.Merge(Snapshot(objectTypes: new() { [0xB1] = "MEMOBJ_SORT" }));

        Assert.Equal(["MEMOBJ_EXECUTE", "MEMOBJ_SORT"], [merged.MemoryObjects[0xB0].Type, merged.MemoryObjects[0xB1].Type]);
    }

    private static MemoryClerkSnapshot Snapshot(Dictionary<ulong, string>? clerks = null,
                                                Dictionary<ulong, string>? objectClerks = null,
                                                Dictionary<ulong, string>? objectTypes = null)
        => new(clerks ?? [], objectClerks ?? [], (objectTypes ?? []).ToDictionary(o => o.Key, o => new MemoryObjectInfo(o.Value, 8192)));

    private static AllocationFunction ClerkPages()
        => new(Inner, AllocationOperation.Allocate, 1, -1, 8192) { IsPage = true, CarriesClerk = true };

    private static CallSpan Span(int callNodeIndex, ulong start, ulong end, uint callIndex)
        => new(1, start, 1, end, start, end, callNodeIndex, 7, callIndex, 1);

    private static ulong PeakWorkspace(TraceTimeline timeline, AllocationIndex allocation)
        => allocation.GetPeakWorkspaceDuring(timeline.Start.Position, timeline.End.Position.Next);

    private static CallFrame Resolved(ulong address,
                                           string module,
                                           string className,
                                           string methodName,
                                           SymbolCategory category = SymbolCategory.Unknown,
                                           string? iterator = null)
        => new()
        {
            Module = new(module),
            Rva = (uint)address,
            Address = address,
            Function = new FrameFunction
            {
                ClassName = className,
                MethodName = methodName,
                SymbolCategory = category,
                Iterator = iterator
            }
        };

    private static AllocationFunction Function(ulong address) => new(address, AllocationOperation.Allocate, 1, -1, 1);

    private static CallFrame Frame(ulong address) => new() { Module = new("sqldk"), Rva = (uint)address, Address = address };

    private static ulong[] Row(ulong sequence, ulong size, int callNodeIndex, ulong returned = 0, ulong receiver = 0)
        => TraceCallRowHelper.Row(sequence,
                             flags: returned != 0 ? ReturnedFlag : 0,
                             rcx: receiver,
                             rdx: size,
                             returnValue: returned,
                             callNodeIndex: callNodeIndex);

    private static ulong[] Chunk(params ulong[][] rows) => TraceCallRowHelper.Chunk(rows);
}
