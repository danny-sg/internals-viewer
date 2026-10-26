using InternalsViewer.Query.FullTrace.Memory.History.Batches;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;

namespace InternalsViewer.UI.App.Tests.Models.Query.Trace.Timeline;

[Trait("Category", "Unit")]
public class VectorRowSourceTests
{
    private const ulong Block = 0x5000;

    private static readonly BatchVector Vector = new(3, Block + 0x40, false, [0x10, 0x20, 0x30], [true, false, true]);

    private static readonly BatchState State = new(0x1000, 900, 2, 0x2000, 0, 0, 0x3000, [2, 0], 3, [Vector]);

    private static object? Hex(BatchVector vector, int row) => BatchSlotFormatter.Format(vector.Slots[row], null);

    [Fact]
    public void Each_Slot_Shows_Its_Offset_In_The_Block()
    {
        var source = new VectorRowSource(State, Vector, Block, Hex);

        Assert.Equal(["0x40", "0x48", "0x50"], Enumerable.Range(0, 3).Select(r => source.Value(r, 2)));
    }

    [Fact]
    public void Each_Slot_Shows_Its_Selection_And_Value()
    {
        var source = new VectorRowSource(State, Vector, Block, Hex);

        Assert.Equal([(object?)1, null, 0], Enumerable.Range(0, 3).Select(r => source.Value(r, 1)));
        Assert.Equal(["0x0000000000000010", null, "0x0000000000000030"], Enumerable.Range(0, 3).Select(r => source.Value(r, 3)));
    }
}
