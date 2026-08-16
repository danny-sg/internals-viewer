using InternalsViewer.Execution.AccessPaths.Aggregation;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.UI.App.ViewModels.Query.Trace;

namespace InternalsViewer.UI.App.Tests.ViewModels.Query.Trace;

public class TraceAggregateViewModelTests
{
    [Fact]
    public void A_Row_Is_Created_For_Each_Aggregate()
    {
        var viewModel = Scalar();

        Assert.Equal(["Expr1003", "Expr1010"], viewModel.Rows.Select(r => r.Column));
        Assert.Equal(["MIN(Id)", "COUNT(*)"], viewModel.Rows.Select(r => r.Expression));

        Assert.All(viewModel.Rows, row => Assert.Equal("NULL", row.Value));
    }

    [Fact]
    public void Syncing_Updates_The_Rows_In_Place()
    {
        var viewModel = Scalar();

        var first = viewModel.Rows[0];

        viewModel.Sync([], [Value("Expr1003", "100"), Value("Expr1010", "11")], string.Empty, 11, 0);

        Assert.Same(first, viewModel.Rows[0]);

        Assert.Equal(["100", "11"], viewModel.Rows.Select(r => r.Value));
        Assert.Equal(11, viewModel.GroupRows);
    }

    [Fact]
    public void Resetting_Returns_Every_Value_To_Null()
    {
        var viewModel = Scalar();

        viewModel.Sync([], [Value("Expr1003", "100"), Value("Expr1010", "11")], string.Empty, 11, 1);

        viewModel.Reset();

        Assert.All(viewModel.Rows, row => Assert.Equal("NULL", row.Value));

        Assert.Equal(0, viewModel.GroupRows);
        Assert.Equal(0, viewModel.Groups);
    }

    [Fact]
    public void A_Grouped_Aggregate_Names_The_Columns_It_Groups_On()
    {
        var viewModel = new TraceAggregateViewModel([new AggregateColumn("Expr1010", AggregateFunction.CountStar)], ["Category"]);

        Assert.True(viewModel.IsGrouped);
        Assert.Equal("Group by Category", viewModel.GroupHeading);

        Assert.Equal(["Category", "Expr1010"], viewModel.Rows.Select(r => r.Column));
        Assert.Equal(["Group By", "COUNT(*)"], viewModel.Rows.Select(r => r.Expression));
    }

    [Fact]
    public void A_Grouped_Aggregate_Shows_The_Group_Column_Value_Alongside_The_Totals()
    {
        var viewModel = new TraceAggregateViewModel([new AggregateColumn("Expr1010", AggregateFunction.CountStar)], ["Category"]);

        viewModel.Sync([Value("Category", "20")], [Value("Expr1010", "5")], "20", 5, 1);

        Assert.Equal(["20", "5"], viewModel.Rows.Select(r => r.Value));
        Assert.Equal("20", viewModel.GroupKey);

        viewModel.Sync([Value("Category", "21")], [Value("Expr1010", "3")], "21", 3, 2);

        Assert.Equal(["21", "3"], viewModel.Rows.Select(r => r.Value));
    }

    private static TraceAggregateViewModel Scalar()
        => new([
                   new AggregateColumn("Expr1003", AggregateFunction.Min) { Argument = new AccessExpression.Column(-1, "Id") },
                   new AggregateColumn("Expr1010", AggregateFunction.CountStar)
               ],
               []);

    private static AggregateValue Value(string column, string value)
        => new(column, string.Empty, value);
}
