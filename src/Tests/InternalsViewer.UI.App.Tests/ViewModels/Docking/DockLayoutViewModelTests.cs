using InternalsViewer.UI.App.ViewModels.Docking;

namespace InternalsViewer.UI.App.Tests.ViewModels.Docking;

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

    private static DocumentViewModel Document(string key)
        => new(key, content: key, viewFactory: () => null!, key: key);
}
