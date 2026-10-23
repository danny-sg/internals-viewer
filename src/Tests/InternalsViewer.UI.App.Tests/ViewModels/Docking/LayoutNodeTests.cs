using InternalsViewer.UI.App.ViewModels.Docking;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Tests.ViewModels.Docking;

[Trait("Category", "Unit")]
[Trait("Area", "Docking")]
public class LayoutNodeTests
{
    private const double Splitter = 4;

    [Fact]
    public void Group_Min_Width_Is_Its_Widest_Document()
    {
        var group = new TabGroupNode(Document("narrow", 100), Document("wide", 455), Document("none", 0));

        Assert.Equal(455, group.GetMinWidth(Splitter));
    }

    [Fact]
    public void Empty_Group_Has_No_Min_Width()
    {
        Assert.Equal(0, new TabGroupNode().GetMinWidth(Splitter));
    }

    [Fact]
    public void Horizontal_Split_Adds_Its_Children_And_The_Splitter()
    {
        var split = new SplitNode(Orientation.Horizontal, new TabGroupNode(Document("left", 100)), new TabGroupNode(Document("right", 455)));

        Assert.Equal(100 + Splitter + 455, split.GetMinWidth(Splitter));
    }

    [Fact]
    public void Vertical_Split_Takes_Its_Wider_Child()
    {
        var split = new SplitNode(Orientation.Vertical, new TabGroupNode(Document("top", 100)), new TabGroupNode(Document("bottom", 455)));

        Assert.Equal(455, split.GetMinWidth(Splitter));
    }

    [Fact]
    public void Fixed_Node_Reports_The_Width_Its_Host_Gave_It()
    {
        var fixedNode = new FixedNode { MinWidth = 320 };

        var split = new SplitNode(Orientation.Horizontal, fixedNode, new TabGroupNode(Document("side", 455)));

        Assert.Equal(320 + Splitter + 455, split.GetMinWidth(Splitter));
    }

    private static DocumentViewModel Document(string key, double minWidth)
        => new(key, content: key, viewFactory: () => null!, key: key) { MinWidth = minWidth };
}
