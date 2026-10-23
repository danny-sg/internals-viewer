using InternalsViewer.UI.App.ViewModels.Docking;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Tests.ViewModels.Docking;

[Trait("Category", "Unit")]
[Trait("Area", "Docking")]
public class DockLayoutViewModelTests
{
    [Fact]
    public void Show_Raises_LayoutChanged_When_It_Adds_A_Document()
    {
        var open = Document("open");
        var closed = Document("closed");

        var dock = new DockLayoutViewModel(new TabGroupNode(open));

        var raised = 0;

        dock.LayoutChanged += (_, _) => raised++;

        dock.Show(closed);

        Assert.Equal(1, raised);
        Assert.True(dock.Contains(closed));
    }

    [Fact]
    public void Show_Does_Not_Raise_LayoutChanged_For_An_Already_Open_Document()
    {
        var first = Document("first");
        var second = Document("second");

        var dock = new DockLayoutViewModel(new TabGroupNode(first, second));

        var raised = 0;

        dock.LayoutChanged += (_, _) => raised++;

        dock.Show(second);

        Assert.Equal(0, raised);
        Assert.Same(second, dock.FindGroup(second)?.SelectedDocument);
    }

    [Fact]
    public void Close_Raises_LayoutChanged()
    {
        var first = Document("first");
        var second = Document("second");

        var dock = new DockLayoutViewModel(new TabGroupNode(first, second));

        var raised = 0;

        dock.LayoutChanged += (_, _) => raised++;

        dock.Close(second);

        Assert.Equal(1, raised);
        Assert.False(dock.Contains(second));
    }

    [Fact]
    public void CloseAll_Closes_Only_Closable_Documents_In_The_Group()
    {
        var fixedDocument = Document("fixed", canClose: false);
        var first = Document("first");
        var second = Document("second");
        var elsewhere = Document("elsewhere");

        var group = new TabGroupNode(fixedDocument, first, second);

        var dock = new DockLayoutViewModel(new SplitNode(Orientation.Horizontal, group, new TabGroupNode(elsewhere)));

        var closed = new List<DocumentViewModel>();

        dock.DocumentClosed += (_, document) => closed.Add(document);

        dock.CloseAll(group);

        Assert.Equal([first, second], closed);
        Assert.Equal([fixedDocument], group.Documents);
        Assert.Same(fixedDocument, group.SelectedDocument);
        Assert.True(dock.Contains(elsewhere));
    }

    [Fact]
    public void CloseAll_Keeps_And_Selects_The_Excepted_Document()
    {
        var first = Document("first");
        var second = Document("second");
        var third = Document("third");

        var group = new TabGroupNode(first, second, third);

        var dock = new DockLayoutViewModel(group);

        var raised = 0;

        dock.LayoutChanged += (_, _) => raised++;

        dock.CloseAll(group, except: second);

        Assert.Equal(1, raised);
        Assert.Equal([second], group.Documents);
        Assert.Same(second, group.SelectedDocument);
    }

    [Fact]
    public void CloseAll_Does_Nothing_Without_Closable_Documents()
    {
        var group = new TabGroupNode(Document("fixed", canClose: false));

        var dock = new DockLayoutViewModel(group);

        var raised = 0;

        dock.LayoutChanged += (_, _) => raised++;

        dock.CloseAll(group);

        Assert.Equal(0, raised);
        Assert.Single(group.Documents);
    }

    [Fact]
    public void NotifySelectionChanged_Raises_SelectionChanged_Only()
    {
        var dock = new DockLayoutViewModel(new TabGroupNode(Document("only")));

        var selectionRaised = 0;
        var layoutRaised = 0;

        dock.SelectionChanged += (_, _) => selectionRaised++;
        dock.LayoutChanged += (_, _) => layoutRaised++;

        dock.NotifySelectionChanged();

        Assert.Equal(1, selectionRaised);
        Assert.Equal(0, layoutRaised);
    }

    [Fact]
    public void Move_Onto_A_Fixed_Node_Edge_Splits_Around_It()
    {
        var document = Document("stack");
        var fixedNode = new FixedNode();

        var dock = new DockLayoutViewModel(new SplitNode(Orientation.Vertical, fixedNode, new TabGroupNode(document)));

        dock.Move(document, fixedNode, DropZone.Left);

        var root = Assert.IsType<SplitNode>(dock.Root);

        Assert.Equal(Orientation.Horizontal, root.Orientation);
        Assert.Same(document, Assert.Single(Assert.IsType<TabGroupNode>(root.First).Documents));
        Assert.Same(fixedNode, root.Second);
        Assert.Same(root, fixedNode.Parent);
    }

    [Fact]
    public void Move_Onto_A_Fixed_Node_Centre_Does_Nothing()
    {
        var document = Document("stack");
        var fixedNode = new FixedNode();
        var group = new TabGroupNode(document);

        var dock = new DockLayoutViewModel(new SplitNode(Orientation.Vertical, fixedNode, group));

        var raised = 0;

        dock.LayoutChanged += (_, _) => raised++;

        dock.Move(document, fixedNode, DropZone.Center);

        Assert.Equal(0, raised);
        Assert.Same(group, dock.FindGroup(document));
    }

    [Fact]
    public void Close_Leaves_The_Fixed_Node_As_Root_When_Its_Only_Group_Empties()
    {
        var document = Document("stack");
        var fixedNode = new FixedNode();

        var dock = new DockLayoutViewModel(new SplitNode(Orientation.Vertical, fixedNode, new TabGroupNode(document)));

        dock.Close(document);

        Assert.Same(fixedNode, dock.Root);
        Assert.Null(fixedNode.Parent);
        Assert.Empty(dock.Groups());
    }

    [Fact]
    public void Show_Adds_Nothing_When_The_Layout_Has_No_Group()
    {
        var document = Document("stack");

        var dock = new DockLayoutViewModel(new FixedNode());

        dock.Show(document);

        Assert.False(dock.Contains(document));
    }

    private static DocumentViewModel Document(string key, bool canClose = true)
        => new(key, content: key, viewFactory: () => null!, canClose: canClose, key: key);
}
