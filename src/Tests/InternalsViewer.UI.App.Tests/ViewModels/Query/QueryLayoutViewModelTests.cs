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

    [Fact]
    public void RestoreRoot_Restores_The_Call_Tree_Saved_Under_Its_Old_Key()
    {
        var layout = new QueryLayoutViewModel(new object());

        Assert.True(layout.RestoreRoot(new DockNode { IsSplit = false, Documents = ["Sql", "Callstack"], Selected = "Sql" }));

        Assert.True(layout.IsVisible("CallTree"));
    }

    [Fact]
    public void A_Full_Trace_Opens_The_Trace_Timeline_As_A_Full_Height_Tab_Beside_The_Sql_Editor()
    {
        var layout = new QueryLayoutViewModel(new object());

        layout.IsTrace = true;

        var group = Assert.IsType<TabGroupNode>(layout.Dock.Root);

        Assert.Equal(["Sql", "FlameChart"], group.Documents.Select(d => d.Key));
        Assert.Equal("Sql", group.SelectedDocument?.Key);
    }

    [Fact]
    public void Leaving_A_Full_Trace_Puts_The_Timeline_Back_Beneath_The_Sql_Editor()
    {
        var layout = new QueryLayoutViewModel(new object());

        layout.IsTrace = true;

        layout.IsTrace = false;

        var split = Assert.IsType<SplitNode>(layout.Dock.Root);

        Assert.Equal(["Sql"], Assert.IsType<TabGroupNode>(split.First).Documents.Select(d => d.Key));
        Assert.Equal(["Timeline"], Assert.IsType<TabGroupNode>(split.Second).Documents.Select(d => d.Key));
    }

    [Fact]
    public void Resetting_A_Full_Trace_Layout_Shows_The_Trace_Timeline_Beside_The_Sql_Editor()
    {
        var layout = new QueryLayoutViewModel(new object());

        layout.IsTrace = true;

        layout.Reset();

        var group = Assert.IsType<TabGroupNode>(layout.Dock.Root);

        Assert.Equal(["Sql", "FlameChart"], group.Documents.Select(d => d.Key));
        Assert.Equal("FlameChart", group.SelectedDocument?.Key);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Showing_A_Call_Tree_Panel_Shows_The_Call_Tree(bool members, bool symbols)
    {
        var layout = new QueryLayoutViewModel(new object());

        layout.IsCallTreeMembersVisible = members;
        layout.IsCallTreeSymbolsVisible = symbols;

        Assert.True(layout.IsCallTreeVisible && layout.IsVisible("CallTree"));
    }
}
