using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Memory;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class TimeTravelMemoryTests
{
    private const ulong Allocator = 0x5000;

    private const ulong Inner = 0x6000;

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

        var (bytes, allocations) = TimeTravelMemory.Apply(log, [Function(Allocator)], null);

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

        var builder = new TimeTravelCallLog.Builder();

        builder.Add(Allocator, 0, Chunk(Row(1, size: 64, node: 1)), 1);
        builder.Add(Inner, 0, Chunk(Row(2, size: 8192, node: 2)), 1);

        var log = builder.Build();

        log.MapNodes([caller, outer, inner]);

        var (bytes, _) = TimeTravelMemory.Apply(log, [Function(Allocator), Function(Inner)], null);

        Assert.Equal(64ul, bytes);
        Assert.Equal(64L, caller.AllocatedBytesIncludingChildren);
    }

    private static MemoryFunction Function(ulong address) => new(address, MemoryOperation.Allocate, 1, -1, 1);

    private static CallstackFrame Frame(ulong address) => new() { Module = "sqldk", Rva = (uint)address, Address = address };

    private static ulong[] Row(ulong sequence, ulong size, int node)
    {
        var row = new ulong[TimeTravelArgumentCall.ValueCount];

        row[0] = sequence;
        row[3] = size;
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
