using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Calls;
using static InternalsViewer.Query.FullTrace.Tests.TraceCallRows;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TraceValueSearchTests
{
    [Fact]
    public async Task A_Value_Is_Found_In_The_Calls_Of_Every_Function()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(1, rcx: 0xA0), Row(2, rcx: 0xB0)), 2);
        builder.Add(0x2000, 0, Chunk(Row(3, rcx: 0xB0)), 1);

        var uses = await new TraceValueSearch(builder.Build()).GetValueUsesAsync(0xB0, CancellationToken.None);

        Assert.Equal([(0x1000ul, "RCX", 1), (0x2000ul, "RCX", 0)], uses.Select(u => (u.Address, u.Location, u.First.Call)));
    }

    [Fact]
    public async Task A_Return_Value_Is_Only_Searched_When_The_Call_Returned()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(1, returnValue: 0xD0), Row(2, flags: TraceCallColumns.ReturnedFlag, returnValue: 0xD0)), 2);

        var use = Assert.Single(await new TraceValueSearch(builder.Build()).GetValueUsesAsync(0xD0, CancellationToken.None));

        Assert.Equal(("RAX", 1, 1), (use.Location, use.Calls, use.First.Call));
    }

    [Fact]
    public async Task Every_Use_In_One_Place_Is_Counted_From_Its_First_Call_To_Its_Last()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk([.. Enumerable.Range(0, 10).Select(i => Row((ulong)i + 100, rcx: 0xA0))]), 10);
        builder.Add(0x1000, 0, Chunk([.. Enumerable.Range(10, 10).Select(i => Row((ulong)i + 100, rcx: 0xA0))]), 10);

        var use = Assert.Single(await new TraceValueSearch(builder.Build()).GetValueUsesAsync(0xA0, CancellationToken.None));

        Assert.Equal(20, use.Calls);
        Assert.Equal((0, At(100)), (use.First.Call, use.First.Position));
        Assert.Equal((19, At(119)), (use.Last.Call, use.Last.Position));
    }

    [Fact]
    public async Task The_Calls_Behind_One_Use_Are_Listed_With_Their_Threads()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(10, flags: 7, rcx: 0xA0), Row(11, flags: 7, rcx: 0xB0)), 2);
        builder.Add(0x1000, 0, Chunk(Row(12, flags: 8 | TraceCallColumns.ReturnedFlag, rcx: 0xA0, returnValue: 0xA0)), 1);

        var search = new TraceValueSearch(builder.Build());

        var use = (await search.GetValueUsesAsync(0xA0, CancellationToken.None)).Single(u => u.Location == "RCX");

        var calls = await search.GetTraceValueCallsAsync(use, 0xA0, CancellationToken.None);

        Assert.Equal([(0, At(10), 7u), (2, At(12), 8u)], calls.Select(c => (c.Call, c.Position, c.Thread)));
    }

    [Fact]
    public async Task Calls_The_Filter_Leaves_Out_Are_Not_Found()
    {
        var tree = new CallStackTree();

        var kept = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10 }, 1);
        var dropped = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x20 }, 1);

        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(1, rcx: 0xA0, node: 0), Row(2, rcx: 0xA0, node: 1), Row(3, rcx: 0xA0, node: 0)), 3);

        var store = builder.Build();

        store.MapNodes([kept, dropped]);

        bool Include(CallStackNode? node) => !ReferenceEquals(node, dropped);

        var search = new TraceValueSearch(store);

        var use = Assert.Single(await search.GetValueUsesAsync(0xA0, CancellationToken.None, Include));

        var calls = await search.GetTraceValueCallsAsync(use, 0xA0, CancellationToken.None, Include);

        Assert.Equal(2, use.Calls);
        Assert.Equal([0, 2], calls.Select(c => c.Call));
        Assert.Equal([0, 2], store.GetIncludedCalls(store.GetCallList(0x1000, 0)!, Include, CancellationToken.None));
    }

    [Fact]
    public async Task Uses_Are_Listed_In_The_Order_They_First_Happened()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(50, rcx: 0xA0)), 1);
        builder.Add(0x2000, 0, Chunk(Row(10, flags: TraceCallColumns.ReturnedFlag, returnValue: 0xA0)), 1);

        var uses = await new TraceValueSearch(builder.Build()).GetValueUsesAsync(0xA0, CancellationToken.None);

        Assert.Equal(["RAX", "RCX"], uses.Select(u => u.Location));
    }
}
