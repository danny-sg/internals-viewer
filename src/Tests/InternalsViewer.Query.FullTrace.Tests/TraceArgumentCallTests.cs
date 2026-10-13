using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.CallLog;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TraceArgumentCallTests
{
    [Fact]
    public void A_Captured_Call_Is_Decoded_From_Its_Values()
    {
        var values = new ulong[TraceArgumentCall.ValueCount];

        values[0] = 99;
        values[1] = 1234u | (1ul << 32);
        values[2] = 0xA0;
        values[4] = 0xC0;
        values[6] = 42;

        var call = TraceArgumentCall.Decode(values);

        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["unsigned __int64 *", "int", "int"], FunctionKind.Static, "int"));

        Assert.Equal(99ul, call.Sequence);
        Assert.Equal(1234u, call.ThreadId);
        Assert.True(call.Returned);
        Assert.Equal(0xA0ul, call.Value(layout.Slots[0]));
        Assert.Equal(0xC0ul, call.Value(layout.Slots[2]));
        Assert.Equal(42ul, call.ReturnValue);
    }

    [Fact]
    public void A_Call_Records_The_Call_Tree_Node_It_Was_Made_On()
    {
        var values = new ulong[TraceArgumentCall.ValueCount];

        values[7] = 7;

        Assert.Equal(7, TraceArgumentCall.Decode(values).Node);
    }

    [Fact]
    public void Only_Integer_Register_Arguments_Are_Captured()
    {
        var call = TraceArgumentCall.Decode(new ulong[TraceArgumentCall.ValueCount]);

        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["double", "int", "int", "int", "int"], FunctionKind.Free, null));

        Assert.Equal([false, true, true, true, false], layout.Slots.Select(TraceArgumentCall.IsCaptured));
        Assert.Null(call.Value(layout.Slots[0]));
        Assert.Null(call.Value(layout.Slots[4]));
    }
}
