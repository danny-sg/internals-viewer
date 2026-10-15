using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Locators;

using static InternalsViewer.Query.FullTrace.Tests.TraceCallRows;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class LocatorHookTests
{
    private const ulong Function = 0x7FF8_0060_0000;

    private const ulong Page336 = 0x0000_0001_0000_0150;

    private const ulong Pointer = 0x21C_0BB4_C180;

    private const ulong Inner = 0x21C_0BB5_0000;

    private const ulong StackPointer = 0x55_47EF_B230;

    private const ulong ReturnAddress = 0x7FF8_0061_0010;

    private const ulong ReturnedFlag = 1ul << 32;

    [Fact]
    public void A_Pointer_Argument_Is_Read_When_The_Call_Is_Made()
    {
        var replay = new FakeTraceReplay().Write(Pointer, Page336);

        var locator = Replay(new LocatorRead(Function, 1, false, LocatorValueType.Page), replay, [0, Pointer, 0, 0]).GetLocator(1);

        Assert.Equal((LocatorValueType.Page, Page336), (locator?.ValueType, locator?.Value));
    }

    [Fact]
    public void A_Path_Is_Followed_Before_The_Value_Is_Read()
    {
        var replay = new FakeTraceReplay().Write(Pointer + 8, Inner).Write(Inner + 0x10, Page336);

        var read = new LocatorRead(Function, 0, false, LocatorValueType.Page, Offset: 0x10, Path: [8]);

        Assert.Equal(Page336, Replay(read, replay, [Pointer, 0, 0, 0]).GetLocator(0)?.Value);
    }

    [Fact]
    public void A_Stack_Argument_Is_Read_Past_The_Return_Address_And_Home_Space()
    {
        var replay = new FakeTraceReplay().Write(StackPointer, ReturnAddress).Write(StackPointer + 8 + 32, 0x77ul);

        var call = Replay(new LocatorRead(Function, 4, false, LocatorValueType.Argument), replay, [0, 0, 0, 0]);

        Assert.Equal(0x77ul, call.GetStackValue(4));
    }

    [Fact]
    public void A_Stack_Argument_Without_A_Return_Address_Is_Read_From_The_Home_Space()
    {
        var replay = new FakeTraceReplay().Write(StackPointer, 0ul).Write(StackPointer + 32, 0x66ul);

        var call = Replay(new LocatorRead(Function, 4, false, LocatorValueType.Argument), replay, [0, 0, 0, 0]);

        Assert.Equal(0x66ul, call.GetStackValue(4));
    }

    [Fact]
    public void A_Return_Value_Is_Read_When_The_Call_Returns()
    {
        var register = LocatorReadResolver.ReturnRegister;

        var replay = new FakeTraceReplay().Write(Pointer, Page336);

        var read = new LocatorRead(Function, register, true, LocatorValueType.Page);

        var store = new TraceCallStore.Builder([read]);

        store.Add(Function, 0, Chunk(Row(1, flags: ReturnedFlag)), 1);

        var hook = new LocatorHook([read], store);

        hook.OnEntry(Call([0, 0, 0, 0]), replay);

        hook.OnReturn(Call([0, 0, 0, 0], returnValue: Pointer), replay);

        Assert.Equal(Page336, store.Build().GetCallList(Function, 0)![0].GetLocator(register)?.Value);
    }

    [Fact]
    public void A_Call_Left_Without_Returning_Reads_Nothing_At_Return()
    {
        var register = LocatorReadResolver.ReturnRegister;

        var read = new LocatorRead(Function, register, true, LocatorValueType.Page);

        var store = new TraceCallStore.Builder([read]);

        store.Add(Function, 0, Chunk(Row(1)), 1);

        var hook = new LocatorHook([read], store);

        var replay = new FakeTraceReplay().Write(Pointer, Page336);

        hook.OnEntry(Call([0, 0, 0, 0]), replay);

        hook.OnAbandoned(Call([0, 0, 0, 0]));

        hook.OnReturn(Call([0, 0, 0, 0], returnValue: Pointer), replay);

        Assert.Null(store.Build().GetCallList(Function, 0)![0].GetLocator(register));
    }

    private static TraceArgumentCall Replay(LocatorRead read, FakeTraceReplay replay, ulong[] arguments)
    {
        var store = new TraceCallStore.Builder([read]);

        store.Add(Function, 0, Chunk(Row(1)), 1);

        new LocatorHook([read], store).OnEntry(Call(arguments), replay);

        return store.Build().GetCallList(Function, 0)![0];
    }

    private static TraceHookCall Call(ulong[] arguments, ulong returnValue = 0)
        => new()
        {
            Function = Function,
            Id = 40,
            Call = 0,
            Arguments = arguments,
            ReturnValue = returnValue,
            StackPointer = StackPointer,
            ReturnAddress = ReturnAddress
        };
}
