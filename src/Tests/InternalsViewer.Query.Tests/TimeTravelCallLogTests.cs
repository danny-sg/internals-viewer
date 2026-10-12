using InternalsViewer.Query.CallStack;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Locators;
using InternalsViewer.Query.CallStack.TimeTravel.Native;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class TimeTravelCallLogTests
{
    private const ulong ReturnedFlag = 1ul << 32;

    private const ulong Page336 = 0x0000_0001_0000_0150;

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

    [Fact]
    public void A_Page_Read_In_The_Replay_Is_Attached_To_Its_Call()
    {
        var builder = new TimeTravelCallLog.Builder([new LocatorRead(0x1000, 2, false, LocatorKind.Page)]);

        builder.Add(0x1000, 0, Chunk(Row(1), Row(2)), 2);

        builder.AddLocators([new TimeTravelPointerValue(0x1000, 0, Page336, 1, 2)]);

        var log = builder.Build();

        var calls = log.CallsOf(0x1000, 0)!;

        Assert.Empty(calls[0].Locators);
        Assert.Equal(new PageAddress(1, 336), calls[1].LocatorAt(2)?.Page);
        Assert.Null(calls[1].LocatorAt(1));
        Assert.Equal(1, log.LocatorCount);
    }

    [Fact]
    public void A_Value_Read_On_Return_Replaces_The_One_Read_When_Called()
    {
        var builder = new TimeTravelCallLog.Builder([new LocatorRead(0x1000, 1, false, LocatorKind.Referenced),
                                                     new LocatorRead(0x1000, 1, true, LocatorKind.Referenced)]);

        builder.Add(0x1000, 0, Chunk(Row(1), Row(2)), 2);

        builder.AddLocators([new TimeTravelPointerValue(0x1000, 0, 3, 0, 1),
                             new TimeTravelPointerValue(0x1000, 0, 3, 1, 1),
                             new TimeTravelPointerValue(0x1000, 0, 5, 1, 1, AtReturn: 1)]);

        var calls = builder.Build().CallsOf(0x1000, 0)!;

        Assert.Equal(3ul, calls[0].LocatorAt(1)?.Value);
        Assert.Equal(5ul, Assert.Single(calls[1].Locators).Value);
    }

    [Fact]
    public void A_Value_Behind_A_Reference_Is_Not_Annotated_On_The_Call_Tree()
    {
        var tree = new CallStackTree();

        var node = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10 }, 1);

        var builder = new TimeTravelCallLog.Builder([new LocatorRead(0x1000, 1, false, LocatorKind.Referenced)]);

        builder.Add(0x1000, 0, Chunk(Row(10, node: 0)), 1);

        builder.AddLocators([new TimeTravelPointerValue(0x1000, 0, 3, 0, 1)]);

        var log = builder.Build();

        log.MapNodes([node]);

        log.AnnotateNodes();

        Assert.False(node.HasLocators);
    }

    [Fact]
    public void A_Value_Read_From_A_Register_With_No_Rule_Is_Ignored()
    {
        var builder = new TimeTravelCallLog.Builder([new LocatorRead(0x1000, 2, false, LocatorKind.Page)]);

        builder.Add(0x1000, 0, Chunk(Row(1)), 1);

        builder.AddLocators([new TimeTravelPointerValue(0x1000, 0, Page336, 0, 1), new TimeTravelPointerValue(0x2000, 0, Page336, 0, 2)]);

        Assert.Equal(0, builder.Build().LocatorCount);
    }

    [Fact]
    public void The_Pages_A_Call_Read_Are_Annotated_On_Its_Call_Tree_Node()
    {
        var tree = new CallStackTree();

        var first = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10 }, 1);
        var second = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x20 }, 1);

        var builder = new TimeTravelCallLog.Builder([new LocatorRead(0x1000, 2, false, LocatorKind.Page)]);

        builder.Add(0x1000, 0, Chunk(Row(10, node: 0), Row(11, node: 1), Row(12, node: 1)), 3);

        builder.AddLocators([new TimeTravelPointerValue(0x1000, 0, Page336, 0, 2),
                             new TimeTravelPointerValue(0x1000, 0, Page336 + 1, 1, 2),
                             new TimeTravelPointerValue(0x1000, 0, Page336 + 2, 2, 2)]);

        var log = builder.Build();

        log.MapNodes([first, second]);

        log.AnnotateNodes();

        Assert.Equal("(1:336)", first.LocatorsLabel);
        Assert.Equal("2 Pages", second.LocatorsLabel);
        Assert.False(tree.Root.HasLocators);
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
