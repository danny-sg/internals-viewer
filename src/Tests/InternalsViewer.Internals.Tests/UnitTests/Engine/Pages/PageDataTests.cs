using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Tests.UnitTests.Engine.Pages;

[Trait("Category", "Unit")]
[Trait("Area", "Engine")]
public class PageDataTests
{
    [Theory]
    [InlineData(0x60, 0)]
    [InlineData(0x85, 0)]
    [InlineData(0x86, 2)]
    [InlineData(0xC0, 1)]
    [InlineData(0x1F00, 1)]
    public void GetSlot_Finds_The_Record_That_Starts_At_Or_Before_The_Offset(int offset, int slot)
    {
        var page = new PageData { OffsetTable = [0x60, 0xAC, 0x86] };

        Assert.Equal(slot, page.GetSlot(offset));
    }

    [Theory]
    [InlineData(0x10)]
    [InlineData(0x5F)]
    public void GetSlot_Finds_No_Record_Before_The_First_One(int offset)
    {
        var page = new PageData { OffsetTable = [0x60, 0xAC] };

        Assert.Null(page.GetSlot(offset));
    }

    [Fact]
    public void GetSlot_Skips_Empty_Slots()
    {
        var page = new PageData { OffsetTable = [0x60, 0, 0x86] };

        Assert.Equal(0, page.GetSlot(0x70));
    }
}
