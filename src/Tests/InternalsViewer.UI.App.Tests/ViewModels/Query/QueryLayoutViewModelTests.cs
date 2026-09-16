using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.ViewModels.Query;

namespace InternalsViewer.UI.App.Tests.ViewModels.Query;

[Trait("Category", "Unit")]
[Trait("Area", "Docking")]
public class QueryLayoutViewModelTests
{
    [Fact]
    public void RestoreRoot_Selects_The_Sql_Editor_Over_The_Saved_Selection()
    {
        var layout = new QueryLayoutViewModel(new object());

        var saved = new DockNode
        {
            IsSplit = false,
            Documents = ["Sql", "Plan", "Events"],
            Selected = "Events"
        };

        Assert.True(layout.RestoreRoot(saved));

        var group = Assert.IsType<TabGroupNode>(layout.Dock.Root);

        Assert.Equal("Sql", group.SelectedDocument?.Key);
    }

    [Fact]
    public void RestoreRoot_Keeps_The_Saved_Selection_In_Groups_Without_The_Sql_Editor()
    {
        var layout = new QueryLayoutViewModel(new object());

        var saved = new DockNode
        {
            IsSplit = true,
            First = new DockNode { Documents = ["Sql", "Plan"], Selected = "Plan" },
            Second = new DockNode { Documents = ["Timeline", "Events"], Selected = "Events" }
        };

        Assert.True(layout.RestoreRoot(saved));

        var split = Assert.IsType<SplitNode>(layout.Dock.Root);

        Assert.Equal("Sql", Assert.IsType<TabGroupNode>(split.First).SelectedDocument?.Key);
        Assert.Equal("Events", Assert.IsType<TabGroupNode>(split.Second).SelectedDocument?.Key);
    }
}
