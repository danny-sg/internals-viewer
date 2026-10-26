using InternalsViewer.Query.FullTrace.Memory.History.Batches;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;

namespace InternalsViewer.UI.App.Tests.Models.Query.Trace.Timeline;

[Trait("Category", "Unit")]
public class BatchRowSourceTests
{
    [Fact]
    public void Selected_Rows_Show_Their_Index_In_The_Selection_Vector()
    {
        var source = new BatchRowSource(State([2, 0], false), 0, Hex);

        Assert.Equal([1, null, 0], Enumerable.Range(0, 3).Select(r => source.Value(r, 1)));
    }

    [Fact]
    public void Slots_Show_As_Hex_And_Unknown_Slots_Are_Empty()
    {
        var source = new BatchRowSource(State([2], false), 0, Hex);

        Assert.Equal(["0x0000000000000010", null, "0x0000000000000030"],
                     Enumerable.Range(0, 3).Select(r => source.Value(r, BatchRowSource.FixedColumnCount)));
    }

    [Fact]
    public void A_New_State_Keeps_The_Layout_Unless_Rows_Purity_Or_The_Opened_Vector_Change()
    {
        var source = new BatchRowSource(State([2], false), 0, Hex);

        Assert.Equal((true, false, false, false),
                     (source.IsSameLayout(State([2], false), 0),
                      source.IsSameLayout(State([1], false), 0),
                      source.IsSameLayout(State([2], true), 0),
                      source.IsSameLayout(State([2], false), 1)));
    }

    private static object? Hex(BatchVector vector, int row) => BatchSlotFormatter.Format(vector.Slots[row], null);

    private static BatchState State(ushort[] selection, bool isPure)
    {
        var rowCount = selection.Max() + 1;

        var vector = new BatchVector(1, 0x5000, isPure, [0x10, 0x20, 0x30], [true, false, true]);

        return new BatchState(0x1000, 900, selection.Length, 0x2000, 0, 0, 0x3000, selection, rowCount, [vector]);
    }
}
