using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.Internals.Tests.UnitTests.Columnstore;

public class RleEntryTests
{
    [Fact]
    public void Classifies_Narrow_Read_Run_By_Sign()
    {
        var entry = new RleEntry(-1, 1500);

        Assert.False(entry.IsValue);

        Assert.Equal(0, entry.BitpackIndex);
    }

    [Fact]
    public void Classifies_Narrow_Repeat_Run_By_Sign()
    {
        var entry = new RleEntry(3, 4500);

        Assert.True(entry.IsValue);
    }

    [Fact]
    public void Classifies_Wide_Read_Run_By_Flag()
    {
        var entry = new RleEntry(-1, 1500, ReadFlag: 1);

        Assert.False(entry.IsValue);

        Assert.Equal(0, entry.BitpackIndex);
    }

    [Fact]
    public void Classifies_Wide_Negative_Value_As_Repeat_When_Flag_Is_Clear()
    {
        var entry = new RleEntry(-5000000000017, 2000, ReadFlag: 0);

        Assert.True(entry.IsValue);
    }

    [Fact]
    public void Recognises_Wide_Terminator()
    {
        var entry = new RleEntry(0, 0, ReadFlag: 0);

        Assert.True(entry.IsTerminator);
    }

    [Fact]
    public void Flags_Variable_Length_Repeat_Run()
    {
        var entry = new RleEntry(RleEntry.VariableLengthRepeatFlag, 5000, IsVariableLengthData: true);

        Assert.True(entry.IsValue);

        Assert.True(entry.HasRepeatFlag);

        Assert.False(entry.IsTerminator);

        Assert.Equal(new SegmentPageSlot(0, 0), entry.PageSlot);
    }

    [Fact]
    public void Reads_Page_Slot_Under_The_Repeat_Flag()
    {
        var entry = new RleEntry(RleEntry.VariableLengthRepeatFlag | (5 << 15) | 3, 100, IsVariableLengthData: true);

        Assert.True(entry.HasRepeatFlag);

        Assert.Equal(new SegmentPageSlot(3, 5), entry.PageSlot);
    }

    [Fact]
    public void Classifies_Variable_Length_Read_Run_By_Sign()
    {
        var entry = new RleEntry(unchecked((int)0x80008000), 15000, IsVariableLengthData: true);

        Assert.False(entry.IsValue);

        Assert.False(entry.HasRepeatFlag);

        Assert.Equal(new SegmentPageSlot(0, 1), entry.PageSlot);
    }
}
