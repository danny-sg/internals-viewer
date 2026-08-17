using InternalsViewer.UI.App.ViewModels.Query.Trace;

namespace InternalsViewer.UI.App.Tests.ViewModels.Query.Trace;

public class TraceSegmentViewModelTests
{
    [Fact]
    public void Each_Grouping_Column_Gets_A_Column_Named_After_It()
    {
        var viewModel = new TraceSegmentViewModel(["Category", "City"]);

        Assert.Equal(["Category", "City"], viewModel.Columns);

        Assert.All(viewModel.Rows, r => Assert.Equal(2, r.Cells.Count));
    }

    [Fact]
    public void A_Window_With_No_Grouping_Columns_Gets_One_Placeholder_Column()
    {
        var viewModel = new TraceSegmentViewModel([]);

        Assert.Equal("(No grouping columns)", Assert.Single(viewModel.Columns));
    }

    [Fact]
    public void The_Two_Keys_Land_In_Their_Own_Rows_Column_By_Column()
    {
        var viewModel = new TraceSegmentViewModel(["Category", "City"]);

        viewModel.Sync(["Bikes", "London"], ["Bikes", "Leeds"], 2);

        Assert.Equal(["Bikes", "London"], viewModel.Rows[0].Cells.Select(c => c.Value));
        Assert.Equal(["Bikes", "Leeds"], viewModel.Rows[1].Cells.Select(c => c.Value));

        Assert.Equal(2, viewModel.Segments);
    }

    [Fact]
    public void Keys_That_Differ_Mark_Both_Rows()
    {
        var viewModel = new TraceSegmentViewModel(["Category"]);

        viewModel.Sync(["Bikes"], ["Wheels"], 2);

        Assert.All(viewModel.Rows, r => Assert.True(r.IsDifferent));
    }

    [Fact]
    public void Keys_That_Match_Mark_Neither_Row()
    {
        var viewModel = new TraceSegmentViewModel(["Category"]);

        viewModel.Sync(["Bikes"], ["Wheels"], 2);
        viewModel.Sync(["Wheels"], ["Wheels"], 2);

        Assert.All(viewModel.Rows, r => Assert.False(r.IsDifferent));
    }

    [Fact]
    public void The_First_Row_Of_All_Differs_Because_No_Segment_Is_Open_Yet()
    {
        var viewModel = new TraceSegmentViewModel(["Category"]);

        viewModel.Sync([], ["Bikes"], 1);

        Assert.All(viewModel.Rows, r => Assert.True(r.IsDifferent));

        Assert.Equal(string.Empty, viewModel.Rows[0].Cells[0].Value);
    }

    [Fact]
    public void A_Window_With_No_Grouping_Columns_Never_Marks_A_Row()
    {
        var viewModel = new TraceSegmentViewModel([]);

        viewModel.Sync([], [], 1);

        Assert.All(viewModel.Rows, r => Assert.False(r.IsDifferent));
    }

    [Fact]
    public void Resetting_Clears_The_Values_And_The_Marking()
    {
        var viewModel = new TraceSegmentViewModel(["Category"]);

        viewModel.Sync(["Bikes"], ["Wheels"], 2);

        viewModel.Reset();

        Assert.Equal(0, viewModel.Segments);

        Assert.All(viewModel.Rows, r => Assert.False(r.IsDifferent));
        Assert.All(viewModel.Rows, r => Assert.All(r.Cells, c => Assert.Equal(string.Empty, c.Value)));
    }
}
