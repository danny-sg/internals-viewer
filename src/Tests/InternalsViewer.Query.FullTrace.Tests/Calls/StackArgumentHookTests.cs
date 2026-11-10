using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;

using static InternalsViewer.Query.FullTrace.Tests.TraceCallRowHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Calls;

[Trait("Category", "Unit")]
public class StackArgumentHookTests
{
    private const ulong Function = 0x7FF8_0060_0000;

    private const ulong StackPointer = 0x55_47EF_B230;

    private const ulong ReturnAddress = 0x7FF8_0061_0010;

    [Fact]
    public void Every_Function_With_Stack_Slots_Is_Hooked_At_Its_Entry()
        => Assert.Equal([new TraceHookFunction(Function, TraceHookEvents.Entry)], new StackArgumentHook(Slots(2), new TraceCallStore.Builder()).Functions);

    [Fact]
    public void A_Stack_Argument_Is_Read_Past_The_Return_Address_And_Home_Space()
    {
        var replay = new FakeTraceReplay().Write(StackPointer, ReturnAddress).Write(StackPointer + 8 + 32, 0x77ul);

        Assert.Equal(0x77ul, Replay(replay, 1).GetStackValue(4));
    }

    [Fact]
    public void A_Stack_Argument_Without_A_Return_Address_Is_Read_From_The_Home_Space()
    {
        var replay = new FakeTraceReplay().Write(StackPointer, 0ul).Write(StackPointer + 32, 0x66ul);

        Assert.Equal(0x66ul, Replay(replay, 1).GetStackValue(4));
    }

    [Fact]
    public void A_Slot_Past_The_Stack_Slots_Of_The_Function_Is_Not_Captured()
    {
        var replay = new FakeTraceReplay().Write(StackPointer, ReturnAddress).Write(StackPointer + 8 + 32, 0x77ul);

        var call = Replay(replay, 1);

        Assert.Null(call.GetStackValue(5));
        Assert.Equal(5, call.IntegerSlots.Length);
    }

    [Fact]
    public void A_Stack_Argument_The_Trace_Cannot_Read_Is_Zero()
    {
        var call = Replay(new FakeTraceReplay(), 2);

        Assert.Equal((0ul, 0ul), (call.GetStackValue(4), call.GetStackValue(5)));
    }

    [Fact]
    public async Task Stack_Arguments_Keep_Their_Call_Across_Chunks_And_Are_Found_By_Value()
    {
        var store = new TraceCallStore.Builder();

        store.Add(Function, 0, Chunk(Row(1), Row(2)), 2);
        store.Add(Function, 0, Chunk(Row(3)), 1);

        store.AddStackArguments(Function, 0, 0, [0x10, 0x20]);
        store.AddStackArguments(Function, 0, 2, [0x30, 0x20]);

        var calls = store.Build().GetCallList(Function, 0)!;

        Assert.Equal([0x10ul, 0x20ul], [calls[0].GetStackValue(4)!.Value, calls[0].GetStackValue(5)!.Value]);
        Assert.Equal([0ul, 0ul], [calls[1].GetStackValue(4)!.Value, calls[1].GetStackValue(5)!.Value]);
        Assert.Equal([0x30ul, 0x20ul], [calls[2].GetStackValue(4)!.Value, calls[2].GetStackValue(5)!.Value]);

        var search = new TraceValueSearch(store.Build());

        var use = Assert.Single(await search.GetValueUsesAsync(0x20, CancellationToken.None));

        Assert.Equal(("[RSP+0x30]", 2, 0, 2), (use.Location, use.CallCount, use.First.CallIndex, use.Last.CallIndex));
    }

    [Fact]
    public void Stack_Arguments_Survive_An_Export_Round_Trip()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(Function, 0, Chunk(Row(1)), 1);

        builder.AddStackArguments(Function, 0, 0, [0x5_0000, 0x6_0000, 0x7_0000]);

        using var stream = new MemoryStream();

        builder.Build().Write(new BinaryWriter(stream));

        stream.Position = 0;

        var call = TraceCallStore.Read(new BinaryReader(stream)).GetCallList(Function, 0)![0];

        Assert.Equal([0x5_0000ul, 0x6_0000ul, 0x7_0000ul], call.IntegerSlots.Skip(4));
    }

    private static AddressTable<int> Slots(int count) => new(new Dictionary<ulong, int> { [Function] = count });

    private static TraceArgumentCall Replay(FakeTraceReplay replay, int slots)
    {
        var store = new TraceCallStore.Builder();

        store.Add(Function, 0, Chunk(Row(1)), 1);

        new StackArgumentHook(Slots(slots), store).OnEntry(Call(), replay);

        return store.Build().GetCallList(Function, 0)![0];
    }

    private static TraceHookCall Call()
        => new()
        {
            FunctionAddress = Function,
            CallId = 40,
            CallIndex = 0,
            Arguments = new ulong[] { 0, 0, 0, 0 },
            StackPointer = StackPointer,
            ReturnAddress = ReturnAddress
        };
}
