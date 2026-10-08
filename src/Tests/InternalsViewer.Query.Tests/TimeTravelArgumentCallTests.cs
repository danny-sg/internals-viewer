using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.TimeTravel;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class TimeTravelArgumentCallTests
{
    [Fact]
    public void A_Captured_Call_Is_Decoded_From_Its_Values()
    {
        var values = new ulong[TimeTravelArgumentCall.ValueCount];

        values[0] = 99;
        values[1] = 1234u | (1ul << 32) | (1ul << 33) | (1ul << (40 + 1)) | (1ul << (48 + 1));
        values[2] = 0xA0;
        values[3] = 0xB0;
        values[6] = 0x77;
        values[11] = (ulong)BitConverter.DoubleToInt64Bits(2.5);
        values[15] = 7;
        values[23] = 8;
        values[30] = 42;

        var call = TimeTravelArgumentCall.From(values);

        var layout = ArgumentLayout.For(new FunctionSignature(["unsigned __int64 *", "double", "int", "int", "int"],
                                                              FunctionKind.Static,
                                                              "int"));

        Assert.Equal(99ul, call.Sequence);
        Assert.Equal(1234u, call.ThreadId);
        Assert.True(call.Returned);
        Assert.Equal(0xA0ul, call.Value(layout.Slots[0]));
        Assert.Equal("2.5", ArgumentValue.Format("double", call.Value(layout.Slots[1])!.Value));
        Assert.Equal(0x77ul, call.Value(layout.Slots[4]));
        Assert.Equal(42ul, call.ReturnValue);
    }

    [Fact]
    public void A_Pointee_Is_Only_Present_When_It_Was_Read()
    {
        var values = new ulong[TimeTravelArgumentCall.ValueCount];

        values[1] = 1ul << 40;
        values[14] = 5;
        values[15] = 6;

        var call = TimeTravelArgumentCall.From(values);

        var layout = ArgumentLayout.For(new FunctionSignature(["int *", "int *"], FunctionKind.Free, null));

        Assert.Equal(5ul, call.Pointee(layout.Slots[0], onReturn: false));
        Assert.Null(call.Pointee(layout.Slots[1], onReturn: false));
        Assert.Null(call.Pointee(layout.Slots[0], onReturn: true));
    }

    [Fact]
    public void A_Call_Records_The_Call_Tree_Node_It_Was_Made_On()
    {
        var values = new ulong[TimeTravelArgumentCall.ValueCount];

        values[32] = 7;

        Assert.Equal(7, TimeTravelArgumentCall.From(values).Node);
    }

    [Fact]
    public void A_Stack_Argument_Is_Unreadable_When_The_Stack_Was_Not_Read()
    {
        var values = new ulong[TimeTravelArgumentCall.ValueCount];

        values[6] = 0x77;

        var call = TimeTravelArgumentCall.From(values);

        var layout = ArgumentLayout.For(new FunctionSignature(["int", "int", "int", "int", "int"], FunctionKind.Free, null));

        Assert.Null(call.Value(layout.Slots[4]));
    }
}
