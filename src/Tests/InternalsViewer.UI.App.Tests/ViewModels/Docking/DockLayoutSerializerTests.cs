using InternalsViewer.UI.App.ViewModels.Docking;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Tests.ViewModels.Docking;

public class DockLayoutSerializerTests
{
    [Fact]
    public void Round_Trips_A_Split_Layout()
    {
        var a = Document("a");
        var b = Document("b");
        var c = Document("c");

        var left = new TabGroupNode(a, b) { SelectedDocument = b };

        var right = new TabGroupNode(c);

        var root = new SplitNode(Orientation.Horizontal, left, right) { FirstStar = 2, SecondStar = 1 };

        var saved = DockLayoutSerializer.Serialize(root);

        var restored = Assert.IsType<SplitNode>(DockLayoutSerializer.Deserialize(saved, Resolver(a, b, c)));

        Assert.Equal(Orientation.Horizontal, restored.Orientation);
        Assert.Equal(2, restored.FirstStar);
        Assert.Equal(1, restored.SecondStar);

        var restoredLeft = Assert.IsType<TabGroupNode>(restored.First);

        Assert.Equal(new[] { "a", "b" }, restoredLeft.Documents.Select(d => d.Key));
        Assert.Same(b, restoredLeft.SelectedDocument);

        var restoredRight = Assert.IsType<TabGroupNode>(restored.Second);

        Assert.Same(c, Assert.Single(restoredRight.Documents));
    }

    [Fact]
    public void Excludes_Non_Persisted_Documents_And_Falls_Back_To_The_First_Selection()
    {
        var kept = Document("kept");
        var transient = Document("transient", persist: false);

        var group = new TabGroupNode(kept, transient) { SelectedDocument = transient };

        var saved = DockLayoutSerializer.Serialize(group);

        Assert.Equal(new[] { "kept" }, saved.Documents);
        Assert.Equal("kept", saved.Selected);
    }

    [Fact]
    public void Collapses_A_Split_When_Only_One_Side_Can_Be_Restored()
    {
        var a = Document("a");
        var b = Document("b");

        var root = new SplitNode(Orientation.Vertical, new TabGroupNode(a), new TabGroupNode(b));

        var saved = DockLayoutSerializer.Serialize(root);

        var restored = Assert.IsType<TabGroupNode>(DockLayoutSerializer.Deserialize(saved, Resolver(a)));

        Assert.Same(a, Assert.Single(restored.Documents));
    }

    [Fact]
    public void Restores_Nothing_When_No_Document_Resolves()
    {
        var saved = DockLayoutSerializer.Serialize(new TabGroupNode(Document("a")));

        Assert.Null(DockLayoutSerializer.Deserialize(saved, Resolver()));
    }

    private static DocumentViewModel Document(string key, bool persist = true)
        => new(key, content: key, viewFactory: () => null!, key: key, persist: persist);

    private static Func<string, DocumentViewModel?> Resolver(params DocumentViewModel[] documents)
        => key => documents.FirstOrDefault(d => d.Key == key);
}
