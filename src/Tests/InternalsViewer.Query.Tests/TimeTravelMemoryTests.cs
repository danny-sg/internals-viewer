using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Memory;
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

        Assert.Equal((1L, 2L, 1L), (summary.Returned, summary.Frees, summary.MatchedFrees));
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

        var purpose = Assert.Single(TimeTravelMemory.Apply(log, [Function(Allocator)], null, []).Purposes);

        var use = Assert.Single(purpose.Uses);

        Assert.Equal(("Other", 150ul, 2L), (purpose.Name, use.Bytes, use.Allocations));
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

        var purpose = Assert.Single(summary.Purposes);

        Assert.Equal((96ul, 64ul, 96ul, 32ul), (purpose.Allocated, purpose.Freed, purpose.PeakInUse, purpose.Held));
        Assert.Equal(96ul, summary.PeakInUse);
    }

    [Fact]
    public void Pages_Stolen_For_An_Operator_Belong_To_The_Operator()
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

        var purpose = Assert.Single(TimeTravelMemory.Apply(log, [new(Allocator, MemoryOperation.Allocate, 1, -1, 8192)], null, [])
                                        .Purposes);

        var use = Assert.Single(purpose.Uses);

        Assert.Equal(("Hash Match Build", "WORKFILE::AllocRowBufFromCrntBuf", "BPool::Steal", 8192ul),
                     (purpose.Name, use.Caller, use.Allocator, use.Bytes));
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

        TimeTravelMemory.Apply(log,
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

    [Theory]
    [InlineData(true, "Hash Match Build")]
    [InlineData(false, "Compilation")]
    public void The_Nearest_Of_Operator_And_Compilation_Owns_The_Memory(bool operatorIsNearer, string expected)
    {
        var tree = new CallStackTree();

        var operatorFrame = Resolved(0x10, "sqlmin", "CQScanHash", "ConsumeBuild", iterator: "Hash Match Build");
        var compileFrame = Resolved(0x20, "sqllang", "CCompPlan", "Compile", SymbolCategory.Compilation);

        var outer = tree.AddCall(tree.Root, operatorIsNearer ? compileFrame : operatorFrame, 1);
        var inner = tree.AddCall(outer, operatorIsNearer ? operatorFrame : compileFrame, 1);
        var allocator = tree.AddCall(inner, Frame(Allocator), 1);

        var builder = new TimeTravelCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 64, node: 2)), 1);

        var log = builder.Build();

        log.MapNodes([outer, inner, allocator]);

        var purpose = Assert.Single(TimeTravelMemory.Apply(log, [Function(Allocator)], null, []).Purposes);

        Assert.Equal(expected, purpose.Name);
    }

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

    private static ulong[] Row(ulong sequence, ulong size, int node, ulong returned = 0)
    {
        var row = new ulong[TimeTravelArgumentCall.ValueCount];

        row[0] = sequence;
        row[1] = returned != 0 ? ReturnedFlag : 0;
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
