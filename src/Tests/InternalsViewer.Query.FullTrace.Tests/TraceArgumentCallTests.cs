using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Arguments;
using static InternalsViewer.Query.FullTrace.Tests.TraceCallRows;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TraceArgumentCallTests
{
    [Fact]
    public void A_Captured_Call_Is_Decoded_From_Its_Values()
    {
        var call = Decode(Row(99, flags: 1234u | TraceCallColumns.ReturnedFlag, rcx: 0xA0, r8: 0xC0, returnValue: 42));

        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["unsigned __int64 *", "int", "int"], FunctionType.Static, "int"));

        Assert.Equal(At(99), call.FromPosition);
        Assert.Equal(1234u, call.ThreadId);
        Assert.True(call.Returned);
        Assert.Equal(0xA0ul, call.GetValue(layout.Slots[0]));
        Assert.Equal(0xC0ul, call.GetValue(layout.Slots[2]));
        Assert.Equal(42ul, call.ReturnValue);
    }

    [Fact]
    public void A_Call_Records_The_Call_Tree_Node_It_Was_Made_On()
    {
        Assert.Equal(7, Decode(Row(1, callNodeIndex: 7)).CallNodeIndex);
    }

    [Fact]
    public void Integer_Register_And_Stack_Arguments_Are_Captured()
    {
        var call = Decode(Row(0));

        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["double", "int", "int", "int", "int"], FunctionType.Free, null));

        Assert.Equal([false, true, true, true, true], layout.Slots.Select(TraceArgumentCall.IsCaptured));
        Assert.Null(call.GetValue(layout.Slots[0]));
        Assert.Null(call.GetValue(layout.Slots[4]));
    }

    private static TraceArgumentCall Decode(ulong[] row) => new TraceCallColumns(row, 1).GetCall(0);
}
