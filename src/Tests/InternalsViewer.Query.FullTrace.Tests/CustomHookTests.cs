using System.Text;
using InternalsViewer.Query.FullTrace.CustomHooks;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class CustomHookTests
{
    private const ulong Function = 0x7FF8_0090_0000;

    private const ulong Pointer = 0x21C_0BB3_CCA0;

    private const ulong StackPointer = 0x55_47EF_B230;

    private const ulong ReturnAddress = 0x7FF8_0091_0010;

    [Fact]
    public void A_Pointer_Argument_Is_Followed_To_Its_Bytes()
    {
        var replay = new FakeTraceReplay().Write(Pointer + 0x24, (byte[])[0x84, 0x03, 0, 0]);

        var capture = Assert.Single(Replay(Definition(register: 1, offset: 0x24, length: 4), replay));

        Assert.Equal((Pointer + 0x24, 3, 9u), (capture.Address, capture.CallIndex, capture.Thread));
        Assert.Equal([0x84, 0x03, 0, 0], capture.Data);
    }

    [Fact]
    public void Bytes_Missing_From_The_Trace_Are_Left_Off()
    {
        var replay = new FakeTraceReplay().Write(Pointer, (byte[])[1, 2, 3]);

        Assert.Equal([1, 2, 3], Assert.Single(Replay(Definition(register: 1, length: 8), replay)).Data);
    }

    [Fact]
    public void A_Stack_Argument_Is_Followed()
    {
        var replay = new FakeTraceReplay().Write(StackPointer, ReturnAddress)
                                          .Write(StackPointer + 8 + 32, Pointer)
                                          .Write(Pointer, (byte[])[7]);

        Assert.Equal([7], Assert.Single(Replay(Definition(register: 4, length: 1), replay)).Data);
    }

    [Fact]
    public void An_Out_Parameter_Is_Read_When_The_Call_Returns()
    {
        var replay = new FakeTraceReplay().Write(Pointer, (byte[])[5, 6]);

        var hook = Hook(Definition(register: 2, length: 2, isReadAtReturn: true));

        hook.OnEntry(Call(), replay);

        Assert.Equal(0, hook.Build().Count);

        hook.OnReturn(Call(), replay);

        Assert.Equal([5, 6], Assert.Single(hook.Build().GetCaptures("Batch")).Data);
    }

    [Fact]
    public void A_Returned_Pointer_Is_Followed()
    {
        var replay = new FakeTraceReplay().Write(Pointer, "REF"u8);

        var hook = Hook(Definition(register: ArgumentLayout.ReturnRegister, length: 3, isReadAtReturn: true));

        hook.OnEntry(Call(), replay);

        hook.OnReturn(Call(returnValue: Pointer), replay);

        Assert.Equal("REF", Encoding.ASCII.GetString(Assert.Single(hook.Build().GetCaptures("Batch")).Data));
    }

    [Fact]
    public void A_Null_Pointer_Captures_Nothing()
    {
        var hook = Hook(Definition(register: 3, length: 4));

        hook.OnEntry(Call(), new FakeTraceReplay());

        Assert.Equal(0, hook.Build().Count);
    }

    private static IReadOnlyList<CustomHookCapture> Replay(CustomHookDefinition definition, FakeTraceReplay replay)
    {
        var hook = Hook(definition);

        hook.OnEntry(Call(), replay);

        return hook.Build().GetCaptures(definition.Name);
    }

    private static CustomHook Hook(CustomHookDefinition definition) => new([(Function, definition)]);

    private static CustomHookDefinition Definition(int register, int offset = 0, int length = 16, bool isReadAtReturn = false)
        => new("Batch", "sqlmin", "D821087A-8893-4F7C-B622-A7A0F3F197C5", 2, 0x900000, "CBpQScan::GetBatch", "data", register,
               isReadAtReturn, offset, length);

    private static TraceHookCall Call(ulong returnValue = 0)
        => new()
        {
            Function = Function,
            Id = 40,
            CallIndex = 3,
            Thread = 9,
            Arguments = new[] { 0ul, Pointer, Pointer, 0ul },
            ReturnValue = returnValue,
            StackPointer = StackPointer,
            ReturnAddress = ReturnAddress
        };
}
