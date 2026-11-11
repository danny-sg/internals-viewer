using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Summary;

namespace InternalsViewer.UI.App.Tests.ViewModels.Query.Trace.Summary;

[Trait("Category", "Unit")]
public class TraceSummaryViewModelTests
{
    [Fact]
    public void Operators_Is_The_Open_Pane_At_First()
    {
        var summary = Create();

        Assert.Equal([true, false], summary.Panes.Select(p => p.IsExpanded));
    }

    [Fact]
    public void Opening_A_Pane_Closes_The_Others()
    {
        var summary = Create();

        summary.Panes[1].Toggle();

        Assert.Equal([false, true], summary.Panes.Select(p => p.IsExpanded));
    }

    [Fact]
    public void Closing_The_Open_Pane_Leaves_None_Open()
    {
        var summary = Create();

        summary.Panes[0].Toggle();

        Assert.Equal([false, false], summary.Panes.Select(p => p.IsExpanded));
    }

    private static TraceSummaryViewModel Create()
        => new(new MemoryHistoryViewModel((_, _) => { }), () => null!);
}
