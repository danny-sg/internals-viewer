using System.Collections.ObjectModel;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.HexView;

namespace InternalsViewer.UI.App.Tests.ViewModels.HexView;

[Trait("Category", "Unit")]
public class HexControlViewModelTests
{
    [Fact]
    public void Selecting_Bytes_Opens_A_Tip_With_Their_Decoded_Values()
    {
        var viewModel = Create();

        viewModel.Select(4, 7);

        var tip = Assert.Single(viewModel.Tips);

        Assert.Same(tip, viewModel.SelectionTip);

        Assert.Equal(4, tip.Start);

        Assert.Equal(7, tip.End);

        Assert.Contains(tip.Values, v => v.DataType == "Int" && v.Value == "1");
    }

    [Fact]
    public void A_New_Selection_Moves_The_Selection_Tip()
    {
        var viewModel = Create();

        viewModel.Select(4, 7);

        viewModel.Select(20, 21);

        var tip = Assert.Single(viewModel.Tips);

        Assert.Equal(20, tip.Start);

        Assert.Contains(tip.Values, v => v.DataType == "SmallInt" && v.Value == "2");
    }

    [Fact]
    public void Pinning_A_Tip_Adds_A_Watch_For_Its_Range()
    {
        var viewModel = Create();

        viewModel.Select(4, 7);

        var tip = viewModel.SelectionTip!;

        viewModel.TogglePin(tip);

        var watch = Assert.Single(viewModel.Watches);

        Assert.Equal(4, watch.Offset);

        Assert.Equal(4, watch.Length);

        Assert.Null(watch.DataType);

        Assert.Same(watch, tip.Watch);

        Assert.True(tip.IsPinned);

        Assert.Null(viewModel.SelectionTip);
    }

    [Fact]
    public void A_Pinned_Tip_Stays_While_A_New_Selection_Opens_Another()
    {
        var viewModel = Create();

        viewModel.Select(4, 7);

        var pinned = viewModel.SelectionTip!;

        viewModel.TogglePin(pinned);

        viewModel.Select(20, 21);

        Assert.Equal(2, viewModel.Tips.Count);

        Assert.Equal(4, pinned.Start);

        Assert.NotSame(pinned, viewModel.SelectionTip);
    }

    [Fact]
    public void Selecting_A_Watched_Range_Opens_No_Second_Tip()
    {
        var viewModel = Create();

        viewModel.Select(4, 7);

        viewModel.TogglePin(viewModel.SelectionTip!);

        viewModel.Select(4, 7);

        Assert.Single(viewModel.Tips);

        Assert.Null(viewModel.SelectionTip);
    }

    [Fact]
    public void Dismissing_Closes_Only_The_Selection_Tip()
    {
        var viewModel = Create();

        viewModel.Select(4, 7);

        var pinned = viewModel.SelectionTip!;

        viewModel.TogglePin(pinned);

        viewModel.Select(20, 21);

        viewModel.Dismiss();

        Assert.Same(pinned, Assert.Single(viewModel.Tips));
    }

    [Fact]
    public void Unpinning_A_Tip_Removes_Its_Watch_And_Closes_It()
    {
        var viewModel = Create();

        viewModel.Select(4, 7);

        var tip = viewModel.SelectionTip!;

        viewModel.TogglePin(tip);

        viewModel.TogglePin(tip);

        Assert.Empty(viewModel.Watches);

        Assert.Empty(viewModel.Tips);
    }

    [Fact]
    public void Pinning_A_Value_Adds_A_Value_Watch()
    {
        var viewModel = Create();

        viewModel.Select(4, 7);

        var tip = viewModel.SelectionTip!;

        var value = tip.Values.First(v => v.DataType == "Int");

        viewModel.TogglePinnedValue(tip, value);

        var watch = Assert.Single(viewModel.Watches);

        Assert.Equal("Int", watch.DataType);

        var valueTip = Assert.Single(viewModel.Tips, t => t.IsValueTip);

        Assert.Same(watch, valueTip.Watch);

        Assert.Equal("1", valueTip.Value);

        Assert.Equal("0x00000004", valueTip.Label);

        Assert.True(value.IsPinned);
    }

    [Fact]
    public void A_Pinned_Value_Stays_When_Its_Tip_Closes()
    {
        var viewModel = Create();

        viewModel.Select(4, 7);

        var tip = viewModel.SelectionTip!;

        viewModel.TogglePinnedValue(tip, tip.Values.First(v => v.DataType == "Int"));

        viewModel.Dismiss();

        Assert.True(Assert.Single(viewModel.Tips).IsValueTip);
    }

    [Fact]
    public void Pinning_A_Value_Leaves_Its_Tip_Unpinned()
    {
        var viewModel = Create();

        viewModel.Select(4, 7);

        var tip = viewModel.SelectionTip!;

        viewModel.TogglePinnedValue(tip, tip.Values.First(v => v.DataType == "Int"));

        Assert.False(tip.IsPinned);

        Assert.Same(tip, viewModel.SelectionTip);
    }

    [Fact]
    public void Pinning_A_Value_Again_Unpins_It()
    {
        var viewModel = Create();

        viewModel.Select(4, 7);

        var tip = viewModel.SelectionTip!;

        var value = tip.Values.First(v => v.DataType == "Int");

        viewModel.TogglePinnedValue(tip, value);

        viewModel.TogglePinnedValue(tip, value);

        Assert.Empty(viewModel.Watches);

        Assert.Same(tip, Assert.Single(viewModel.Tips));

        Assert.False(value.IsPinned);
    }

    [Fact]
    public void Unpinning_A_Value_Tip_Clears_The_Pin_On_Its_Row()
    {
        var viewModel = Create();

        viewModel.Select(4, 7);

        var tip = viewModel.SelectionTip!;

        var value = tip.Values.First(v => v.DataType == "Int");

        viewModel.TogglePinnedValue(tip, value);

        viewModel.TogglePin(viewModel.Tips.First(t => t.IsValueTip));

        Assert.False(value.IsPinned);
    }

    [Fact]
    public void Reselecting_A_Range_Shows_Its_Pinned_Values()
    {
        var viewModel = Create();

        viewModel.Select(4, 7);

        var tip = viewModel.SelectionTip!;

        viewModel.TogglePinnedValue(tip, tip.Values.First(v => v.DataType == "Int"));

        viewModel.Select(20, 21);

        viewModel.Select(4, 7);

        Assert.True(viewModel.SelectionTip!.Values.First(v => v.DataType == "Int").IsPinned);
    }

    [Fact]
    public void Clearing_Pins_Empties_The_Watches_And_Keeps_The_Selection_Tip()
    {
        var viewModel = Create();

        viewModel.Select(4, 7);

        viewModel.TogglePin(viewModel.SelectionTip!);

        viewModel.Select(20, 21);

        var selection = viewModel.SelectionTip!;

        viewModel.TogglePinnedValue(selection, selection.Values.First(v => v.DataType == "SmallInt"));

        viewModel.ClearPins();

        Assert.Empty(viewModel.Watches);

        Assert.Same(selection, Assert.Single(viewModel.Tips));

        Assert.False(viewModel.HasPins);

        Assert.All(selection.Values, v => Assert.False(v.IsPinned));
    }

    [Fact]
    public void Setting_Watches_Opens_A_Tip_For_Each()
    {
        var viewModel = Create();

        viewModel.SetWatches([new HexWatch(4, 4) { Label = "Row Count" }, new HexWatch(20, 2, "SmallInt")]);

        Assert.Equal(2, viewModel.Tips.Count);

        var tip = viewModel.Tips.First(t => !t.IsValueTip);

        Assert.Equal("Row Count", tip.Label);

        Assert.Contains(tip.Values, v => v.DataType == "Int" && v.Value == "1");

        Assert.Equal("2", viewModel.Tips.First(t => t.IsValueTip).Value);
    }

    [Fact]
    public void Replacing_The_Watches_Swaps_The_Tips()
    {
        var viewModel = Create();

        ObservableCollection<HexWatch> first = [new HexWatch(4, 4)];

        viewModel.SetWatches(first);

        viewModel.SetWatches([new HexWatch(20, 2)]);

        first.Add(new HexWatch(0, 1));

        Assert.Equal(20, Assert.Single(viewModel.Tips).Start);
    }

    [Fact]
    public void A_Watch_Added_By_The_Owner_Opens_A_Tip()
    {
        var viewModel = Create();

        ObservableCollection<HexWatch> watches = [];

        viewModel.SetWatches(watches);

        watches.Add(new HexWatch(4, 4, "Int"));

        Assert.Equal("1", Assert.Single(viewModel.Tips).Value);
    }

    [Fact]
    public void Labelling_A_Pinned_Tip_Names_Its_Watch()
    {
        var viewModel = Create();

        viewModel.Select(4, 7);

        var tip = viewModel.SelectionTip!;

        viewModel.Pin(tip);

        tip.SetLabel("Row Count");

        Assert.Equal("Row Count", Assert.Single(viewModel.Watches).Label);
    }

    [Fact]
    public void A_Detached_Control_Picks_Up_Watches_Added_Meanwhile_When_It_Attaches()
    {
        var viewModel = Create();

        ObservableCollection<HexWatch> watches = [];

        viewModel.SetWatches(watches);

        viewModel.Detach();

        watches.Add(new HexWatch(4, 4));

        Assert.Empty(viewModel.Tips);

        viewModel.Attach();

        Assert.Single(viewModel.Tips);
    }

    [Fact]
    public void New_Data_Decodes_The_Bytes_Now_Under_Each_Tip()
    {
        var viewModel = Create();

        viewModel.Select(4, 7);

        var tip = viewModel.SelectionTip!;

        viewModel.TogglePinnedValue(tip, tip.Values.First(v => v.DataType == "Int"));

        viewModel.SetData([0, 0, 0, 0, 2, 0, 0, 0], 0);

        Assert.Contains(tip.Values, v => v.DataType == "Int" && v.Value == "2");

        Assert.Equal("2", viewModel.Tips.First(t => t.IsValueTip).Value);
    }

    [Fact]
    public void Data_That_Misses_A_Tip_Keeps_Its_Values()
    {
        var viewModel = Create();

        viewModel.Select(4, 7);

        var tip = viewModel.SelectionTip!;

        viewModel.SetData([9, 9, 9, 9], 16);

        Assert.Contains(tip.Values, v => v.DataType == "Int" && v.Value == "1");
    }

    private static HexControlViewModel Create()
    {
        var viewModel = new HexControlViewModel();

        viewModel.SetData([0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0], 0);

        return viewModel;
    }
}
