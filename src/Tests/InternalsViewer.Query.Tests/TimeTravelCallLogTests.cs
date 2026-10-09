using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.TimeTravel.CallLog;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class TimeTravelCallLogTests
{
    private const ulong ReturnedFlag = 1ul << 32;

    [Fact]
    public void The_Calls_Of_One_Function_On_One_Instance_Are_Read_Back_In_Order()
    {
        var builder = new TimeTravelCallLog.Builder();

        builder.Add(0x1000, 0, Chunk(Row(7, rcx: 0xA0)), 1);
        builder.Add(0x2000, 0xB0, Chunk(Row(8, rcx: 0xB0), Row(9, rcx: 0xB0)), 2);

        var calls = builder.Build().CallsOf(0x2000, 0xB0);

        Assert.NotNull(calls);
        Assert.Equal([8ul, 9ul], calls.Select(c => c.Sequence));
        Assert.All(calls, c => Assert.Equal(0xB0ul, c.IntegerSlots[0]));
    }

    [Fact]
    public void Calls_Logged_In_Several_Chunks_Read_As_One_List()
    {
        var builder = new TimeTravelCallLog.Builder();

        builder.Add(0x1000, 0, Chunk(Row(1), Row(2)), 2);
        builder.Add(0x1000, 0, Chunk(Row(3)), 1);

        var calls = builder.Build().CallsOf(0x1000, 0);

        Assert.NotNull(calls);
        Assert.Equal(3, calls.Count);
        Assert.Equal(3ul, calls[2].Sequence);
        Assert.Equal([1ul, 2ul, 3ul], calls.Select(c => c.Sequence));
    }

    [Fact]
    public void A_Function_With_No_Logged_Calls_Reads_As_Nothing()
    {
        var builder = new TimeTravelCallLog.Builder();

        builder.Add(0x1000, 0, Chunk(Row(7, rcx: 0xA0)), 1);

        Assert.Null(builder.Build().CallsOf(0x1000, 0xB0));
    }

    [Fact]
    public void Repeated_Values_Compress_Well()
    {
        var builder = new TimeTravelCallLog.Builder();

        var calls = 1_000;

        builder.Add(0x1000, 0, Chunk([.. Enumerable.Range(0, calls).Select(i => Row((ulong)i, rcx: 0xA0))]), calls);

        var log = builder.Build();

        Assert.Equal(calls, log.Calls);
        Assert.True(log.Size < calls * TimeTravelArgumentCall.ValueCount * sizeof(ulong) / 10);
    }

    [Fact]
    public async Task A_Value_Is_Found_In_The_Calls_Of_Every_Function()
    {
        var builder = new TimeTravelCallLog.Builder();

        builder.Add(0x1000, 0, Chunk(Row(1, rcx: 0xA0), Row(2, rcx: 0xB0)), 2);
        builder.Add(0x2000, 0, Chunk(Row(3, rcx: 0xB0)), 1);

        var uses = await builder.Build().FindAsync(0xB0, CancellationToken.None);

        Assert.Equal([(0x1000ul, "RCX", 1), (0x2000ul, "RCX", 0)], uses.Select(u => (u.Address, u.Location, u.First.Call)));
    }

    [Fact]
    public async Task A_Return_Value_Is_Only_Searched_When_The_Call_Returned()
    {
        var builder = new TimeTravelCallLog.Builder();

        builder.Add(0x1000, 0, Chunk(Row(1, returnValue: 0xD0), Row(2, flags: ReturnedFlag, returnValue: 0xD0)), 2);

        var use = Assert.Single(await builder.Build().FindAsync(0xD0, CancellationToken.None));

        Assert.Equal(("RAX", 1, 1), (use.Location, use.Calls, use.First.Call));
    }

    [Fact]
    public async Task Every_Use_In_One_Place_Is_Counted_From_Its_First_Call_To_Its_Last()
    {
        var builder = new TimeTravelCallLog.Builder();

        builder.Add(0x1000, 0, Chunk([.. Enumerable.Range(0, 10).Select(i => Row((ulong)i + 100, rcx: 0xA0))]), 10);
        builder.Add(0x1000, 0, Chunk([.. Enumerable.Range(10, 10).Select(i => Row((ulong)i + 100, rcx: 0xA0))]), 10);

        var use = Assert.Single(await builder.Build().FindAsync(0xA0, CancellationToken.None));

        Assert.Equal(20, use.Calls);
        Assert.Equal((0, 100ul), (use.First.Call, use.First.Sequence));
        Assert.Equal((19, 119ul), (use.Last.Call, use.Last.Sequence));
    }

    [Fact]
    public async Task Uses_Are_Listed_In_The_Order_They_First_Happened()
    {
        var builder = new TimeTravelCallLog.Builder();

        builder.Add(0x1000, 0, Chunk(Row(50, rcx: 0xA0)), 1);
        builder.Add(0x2000, 0, Chunk(Row(10, flags: ReturnedFlag, returnValue: 0xA0)), 1);

        var uses = await builder.Build().FindAsync(0xA0, CancellationToken.None);

        Assert.Equal(["RAX", "RCX"], uses.Select(u => u.Location));
    }

    [Fact]
    public async Task A_Call_Leads_To_The_Call_Tree_Node_It_Was_Made_On()
    {
        var tree = new CallStackTree();

        var first = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10 }, 1);
        var second = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x20 }, 1);

        var builder = new TimeTravelCallLog.Builder();

        builder.Add(0x1000, 0, Chunk(Row(1, rcx: 0xA0, node: 1), Row(2, node: -1)), 2);

        var log = builder.Build();

        log.MapNodes([first, second]);

        var calls = log.CallsOf(0x1000, 0)!;

        Assert.Same(second, log.NodeOf(calls[0]));
        Assert.Null(log.NodeOf(calls[1]));
        Assert.Same(second, Assert.Single(await log.FindAsync(0xA0, CancellationToken.None)).First.Node);
    }

    private static ulong[] Row(ulong sequence,
                               ulong flags = 0,
                               ulong rcx = 0,
                               ulong returnValue = 0,
                               int node = 0)
    {
        var row = new ulong[TimeTravelArgumentCall.ValueCount];

        row[0] = sequence;
        row[1] = flags;
        row[2] = rcx;
        row[6] = returnValue;
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
