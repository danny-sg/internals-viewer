using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Page;

namespace InternalsViewer.UI.App.Tests.ViewModels.Page;

[Trait("Category", "Unit")]
public class HexTipViewModelTests
{
    [Fact]
    public void A_Tip_Is_Labelled_With_Its_Offsets()
    {
        var tip = new HexTipViewModel();

        tip.Describe(4, 7, [1, 0, 0, 0]);

        Assert.Equal("4 (0x00000004) - 7 (0x00000007)", tip.Label);
    }

    [Fact]
    public void A_Tip_Over_One_Byte_Is_Labelled_With_One_Offset()
    {
        var tip = new HexTipViewModel();

        tip.Describe(4, 4, [1]);

        Assert.Equal("4 (0x00000004)", tip.Label);
    }

    [Fact]
    public void A_Label_Written_Over_The_Offsets_Is_Kept()
    {
        var tip = new HexTipViewModel { CustomLabel = "Slot Count" };

        tip.Describe(4, 7, [1, 0, 0, 0]);

        Assert.Equal("Slot Count", tip.Label);
    }

    [Fact]
    public void Clearing_A_Written_Label_Goes_Back_To_The_Offsets()
    {
        var tip = new HexTipViewModel { CustomLabel = "Slot Count" };

        tip.Describe(4, 4, [1]);

        tip.CustomLabel = null;

        Assert.Equal("4 (0x00000004)", tip.Label);
    }

    [Fact]
    public void A_Value_Tip_Can_Be_Labelled()
    {
        var tip = new HexTipViewModel("Int") { CustomLabel = "Row Count" };

        tip.Describe(4, 7, [1, 0, 0, 0]);

        Assert.Equal("Row Count", tip.Label);

        Assert.Equal("1", tip.Value);
    }

    [Fact]
    public void A_Value_Tip_Is_Labelled_With_Its_Address()
    {
        var tip = new HexTipViewModel("Int");

        tip.Describe(4, 7, [1, 0, 0, 0]);

        Assert.Equal("0x00000004", tip.Label);
    }

    [Fact]
    public void Setting_A_Label_Writes_Over_The_Default()
    {
        var tip = new HexTipViewModel("Int");

        tip.Describe(4, 7, [1, 0, 0, 0]);

        tip.SetLabel("  Row Count ");

        Assert.Equal("Row Count", tip.Label);

        Assert.Equal("Row Count", tip.CustomLabel);
    }

    [Fact]
    public void Setting_A_Blank_Label_Goes_Back_To_The_Default()
    {
        var tip = new HexTipViewModel("Int") { CustomLabel = "Row Count" };

        tip.Describe(4, 7, [1, 0, 0, 0]);

        tip.SetLabel(" ");

        Assert.Equal("0x00000004", tip.Label);

        Assert.Null(tip.CustomLabel);
    }

    [Fact]
    public void Setting_The_Default_Label_Keeps_It_Following_The_Range()
    {
        var tip = new HexTipViewModel();

        tip.Describe(4, 4, [1]);

        tip.SetLabel("4 (0x00000004)");

        tip.Describe(8, 8, [1]);

        Assert.Null(tip.CustomLabel);

        Assert.Equal("8 (0x00000008)", tip.Label);
    }

    [Fact]
    public void A_Tip_From_A_Watch_Takes_Its_Range_And_Label()
    {
        var tip = new HexTipViewModel(new HexWatch(40, 2, "SmallInt") { Label = "Row Count" });

        Assert.Equal(40, tip.Start);

        Assert.Equal(41, tip.End);

        Assert.Equal("SmallInt", tip.DataType);

        Assert.Equal("Row Count", tip.Label);

        Assert.True(tip.IsPinned);
    }

    [Fact]
    public void Labelling_A_Watched_Tip_Names_The_Watch()
    {
        var watch = new HexWatch(40, 2, "SmallInt");

        var tip = new HexTipViewModel(watch);

        tip.SetLabel("Row Count");

        Assert.Equal("Row Count", watch.Label);

        tip.SetLabel(string.Empty);

        Assert.Null(watch.Label);
    }

    [Fact]
    public void A_Value_Tip_Shows_Only_Its_Own_Type()
    {
        var tip = new HexTipViewModel("SmallInt");

        tip.Describe(0, 1, [0, 1]);

        Assert.Equal("256", tip.Value);

        Assert.Empty(tip.Values);
    }

    [Fact]
    public void Decoding_The_Same_Types_Updates_The_Values_In_Place()
    {
        var tip = new HexTipViewModel();

        tip.Describe(4, 7, [1, 0, 0, 0]);

        var value = tip.Values.First(v => v.DataType == "Int");

        tip.Decode([2, 0, 0, 0]);

        Assert.Same(value, tip.Values.First(v => v.DataType == "Int"));

        Assert.Equal("2", value.Value);
    }
}
