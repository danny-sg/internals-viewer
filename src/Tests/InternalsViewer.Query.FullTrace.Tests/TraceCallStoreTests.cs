using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.CallStack.Locators;

using static InternalsViewer.Query.FullTrace.Tests.TraceCallRows;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TraceCallStoreTests
{
    private const ulong ReturnedFlag = TraceCallColumns.ReturnedFlag;

    private const ulong Page336 = 0x0000_0001_0000_0150;

    [Fact]
    public void The_Calls_Of_One_Function_On_One_Instance_Are_Read_Back_In_Order()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(7, rcx: 0xA0)), 1);
        builder.Add(0x2000, 0xB0, Chunk(Row(8, rcx: 0xB0), Row(9, rcx: 0xB0)), 2);

        var calls = builder.Build().GetCallList(0x2000, 0xB0);

        Assert.NotNull(calls);
        Assert.Equal([At(8), At(9)], calls.Select(c => c.FromPosition));
        Assert.All(calls, c => Assert.Equal(0xB0ul, c.IntegerSlots[0]));
    }

    [Fact]
    public void Calls_Logged_In_Several_Chunks_Read_As_One_List()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(1), Row(2)), 2);
        builder.Add(0x1000, 0, Chunk(Row(3)), 1);

        var calls = builder.Build().GetCallList(0x1000, 0);

        Assert.NotNull(calls);
        Assert.Equal(3, calls.Count);
        Assert.Equal(At(3), calls[2].FromPosition);
        Assert.Equal([At(1), At(2), At(3)], calls.Select(c => c.FromPosition));
    }

    [Fact]
    public void A_Function_With_No_Logged_Calls_Reads_As_Nothing()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(7, rcx: 0xA0)), 1);

        Assert.Null(builder.Build().GetCallList(0x1000, 0xB0));
    }

    [Fact]
    public void Repeated_Values_Compress_Well()
    {
        var builder = new TraceCallStore.Builder();

        var calls = 1_000;

        builder.Add(0x1000, 0, Chunk([.. Enumerable.Range(0, calls).Select(i => Row((ulong)i, rcx: 0xA0))]), calls);

        var store = builder.Build();

        Assert.Equal(calls, store.Calls);
        Assert.True(store.Size < calls * TraceCallColumns.ValueCount * sizeof(ulong) / 10);
    }

    [Fact]
    public void A_Call_Carries_Where_It_Started_And_Ended()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(41, steps: 3, endSequence: 57, endSteps: 9)), 1);

        var call = builder.Build().GetCallList(0x1000, 0)![0];

        Assert.Equal((At(41, 3), At(57, 9)), (call.FromPosition, call.ToPosition));
    }

    [Fact]
    public void A_Call_Still_Running_Has_No_End()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(41)), 1);

        Assert.False(builder.Build().GetCallList(0x1000, 0)![0].ToPosition.IsKnown);
    }

    [Fact]
    public void An_End_Reported_After_Its_Chunk_Was_Sent_Is_Applied()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(1), Row(2)), 2);
        builder.Add(0x1000, 0, Chunk(Row(3)), 1);

        builder.AddToPosition(0x1000, 0, 2, 90, 4, 0x42, 1);

        var call = builder.Build().GetCallList(0x1000, 0)![2];

        Assert.Equal((At(90, 4), true, 0x42ul), (call.ToPosition, call.Returned, call.ReturnValue));
    }

    [Fact]
    public void A_Call_Reads_Its_Start_And_Thread_Across_Chunks()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(3, flags: 7), Row(8, flags: 7)), 2);
        builder.Add(0x1000, 0, Chunk(Row(20, flags: 5)), 1);

        var calls = builder.Build().GetCallList(0x1000, 0)!;

        Assert.Equal([(At(3), 7u), (At(8), 7u), (At(20), 5u)],
                     Enumerable.Range(0, 3).Select(i => (calls.GetFromPosition(i), calls.GetThread(i))));
    }

    [Fact]
    public void The_Enclosing_Call_Is_The_Latest_Started_On_The_Thread_That_Has_Not_Ended()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(3, endSequence: 40), Row(5, endSequence: 9), Row(50)), 3);

        var calls = builder.Build().GetCallList(0x1000, 0)!;

        Assert.Equal([0, 1, 0, -1, 2], new ulong[] { 4, 7, 12, 2, 60 }.Select(s => calls.IndexOfEnclosing(At(s), 0)));
        Assert.Equal(-1, calls.IndexOfEnclosing(At(7), 5));
    }

    [Fact]
    public void A_String_Is_Kept_When_Written()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(1)), 1);

        builder.AddLocator(0x1000, 0, 0, new Locator(1, LocatorValueType.WideText, 10, 0, "dbo.Orders"), false);

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            builder.Build().Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        var locator = TraceCallStore.Read(reader).GetCallList(0x1000, 0)![0].GetLocator(1);

        Assert.Equal((LocatorValueType.WideText, "dbo.Orders"), (locator?.ValueType, locator?.ToString()));
    }

    [Fact]
    public void Bindings_Are_Kept_When_Written()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(1)), 1);

        builder.AddLocator(0x1000, 0, 0, new Locator(3, LocatorValueType.Bindings, 1, 1, "1,3,1,0,0,0"), false);

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            builder.Build().Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        var locator = TraceCallStore.Read(reader).GetCallList(0x1000, 0)![0].GetLocator(3);

        Assert.Equal((LocatorValueType.Bindings, "#1 I4 Value +0x0"), (locator?.ValueType, locator?.ToString()));
    }

    [Fact]
    public async Task A_Stack_Argument_Is_Kept_As_A_Value_Beside_The_Page_It_Points_To()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(1), Row(2)), 2);

        builder.AddLocator(0x1000, 0, 1, new Locator(4, LocatorValueType.Argument, 0x5_0000), false);
        builder.AddLocator(0x1000, 0, 1, new Locator(4, LocatorValueType.Page, Page336), false);

        var store = builder.Build();

        var call = store.GetCallList(0x1000, 0)![1];

        Assert.Equal(0x5_0000ul, call.GetStackValue(4));
        Assert.Equal(new PageAddress(1, 336), call.GetLocator(4)?.Page);

        var search = new TraceValueSearch(store);

        var use = Assert.Single(await search.GetValueUsesAsync(0x5_0000, CancellationToken.None));

        Assert.Equal(("[RSP+0x28]", 1), (use.Location, use.First.CallIndex));
        Assert.Equal([1], (await search.GetTraceValueCallsAsync(use, 0x5_0000, CancellationToken.None)).Select(c => c.CallIndex));
    }

    [Fact]
    public async Task A_Call_Leads_To_The_Call_Tree_Node_It_Was_Made_On()
    {
        var tree = new CallStackTree();

        var first = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10 }, 1);
        var second = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x20 }, 1);

        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(1, rcx: 0xA0, callNodeIndex: 1), Row(2, callNodeIndex: -1)), 2);

        var store = builder.Build();

        store.MapNodes([first, second]);

        var calls = store.GetCallList(0x1000, 0)!;

        Assert.Same(second, store.GetNode(calls[0]));
        Assert.Null(store.GetNode(calls[1]));
        Assert.Same(second, Assert.Single(await new TraceValueSearch(store).GetValueUsesAsync(0xA0, CancellationToken.None)).First.Node);
    }

    [Fact]
    public void A_Page_Read_In_The_Replay_Is_Attached_To_Its_Call()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(1), Row(2)), 2);

        builder.AddLocator(0x1000, 0, 1, new Locator(2, LocatorValueType.Page, Page336), false);

        var store = builder.Build();

        var calls = store.GetCallList(0x1000, 0)!;

        Assert.Empty(calls[0].Locators);
        Assert.Equal(new PageAddress(1, 336), calls[1].GetLocator(2)?.Page);
        Assert.Null(calls[1].GetLocator(1));
        Assert.Equal(1, store.LocatorCount);
    }

    [Fact]
    public void A_Value_Read_On_Return_Replaces_The_One_Read_When_Called()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(1), Row(2)), 2);

        builder.AddLocator(0x1000, 0, 0, new Locator(1, LocatorValueType.Referenced, 3), false);
        builder.AddLocator(0x1000, 0, 1, new Locator(1, LocatorValueType.Referenced, 3), false);
        builder.AddLocator(0x1000, 0, 1, new Locator(1, LocatorValueType.Referenced, 5), true);

        var calls = builder.Build().GetCallList(0x1000, 0)!;

        Assert.Equal(3ul, calls[0].GetLocator(1)?.Value);
        Assert.Equal(5ul, Assert.Single(calls[1].Locators).Value);
    }

    [Fact]
    public void A_Value_Behind_A_Reference_Is_Not_Annotated_On_The_Call_Tree()
    {
        var tree = new CallStackTree();

        var node = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10 }, 1);

        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(10, callNodeIndex: 0)), 1);

        builder.AddLocator(0x1000, 0, 0, new Locator(1, LocatorValueType.Referenced, 3), false);

        var store = builder.Build();

        store.MapNodes([node]);

        store.AnnotateNodes();

        Assert.False(node.HasLocators);
    }

    [Fact]
    public void The_Pages_A_Call_Read_Are_Annotated_On_Its_Call_Tree_Node()
    {
        var tree = new CallStackTree();

        var first = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10 }, 1);
        var second = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x20 }, 1);

        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(10, callNodeIndex: 0), Row(11, callNodeIndex: 1), Row(12, callNodeIndex: 1)), 3);

        builder.AddLocator(0x1000, 0, 0, new Locator(2, LocatorValueType.Page, Page336), false);
        builder.AddLocator(0x1000, 0, 1, new Locator(2, LocatorValueType.Page, Page336 + 1), false);
        builder.AddLocator(0x1000, 0, 2, new Locator(2, LocatorValueType.Page, Page336 + 2), false);

        var store = builder.Build();

        store.MapNodes([first, second]);

        store.AnnotateNodes();

        Assert.Equal("(1:336)", first.LocatorsLabel);
        Assert.Equal("2 Pages", second.LocatorsLabel);
        Assert.False(tree.Root.HasLocators);
    }



    private static byte[] ToEightSlots(byte[] written)
    {
        Assert.Equal(-2, BitConverter.ToInt32(written));

        var bytes = written[sizeof(int)..];

        byte[] returned = [(byte)ArgumentLayout.ReturnRegister, 0, 0, 0, (byte)LocatorValueType.Page];

        var register = bytes.AsSpan().LastIndexOf(returned);

        bytes[register] = 8;

        return bytes;
    }
}
