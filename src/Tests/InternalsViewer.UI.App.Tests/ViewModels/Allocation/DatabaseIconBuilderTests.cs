using System.Drawing;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Allocation;

namespace InternalsViewer.UI.App.Tests.ViewModels.Allocation;

[Trait("Category", "Unit")]
[Trait("Area", "Allocation")]
public class DatabaseIconBuilderTests
{
    [Fact]
    public void Cells_Are_Filled_In_Proportion_To_Page_Count()
    {
        var cells = DatabaseIconBuilder.Build([Layer("A", Color.Red, 600), Layer("B", Color.Blue, 300)]);

        Assert.Equal(9, cells.Count);

        Assert.Equal(6, cells.Count(c => c == Color.Red));
        Assert.Equal(3, cells.Count(c => c == Color.Blue));

        Assert.All(cells.Take(6), c => Assert.Equal(Color.Red, c));
    }

    [Fact]
    public void The_Largest_Object_Is_Filled_First()
    {
        var cells = DatabaseIconBuilder.Build([Layer("A", Color.Red, 100), Layer("B", Color.Blue, 800)]);

        Assert.Equal(Color.Blue, cells[0]);
        Assert.Equal(Color.Red, cells[8]);
    }

    [Fact]
    public void A_Dominant_Object_Leaves_The_Last_Two_Cells_To_Others()
    {
        var cells = DatabaseIconBuilder.Build([Layer("A", Color.Red, 10000),
                                               Layer("B", Color.Blue, 5),
                                               Layer("C", Color.Green, 3)]);

        Assert.Equal(7, cells.Count(c => c == Color.Red));

        Assert.Equal(Color.Blue, cells[7]);
        Assert.Equal(Color.Green, cells[8]);
    }

    [Fact]
    public void A_Dominant_Object_Leaves_One_Cell_When_There_Is_Only_One_Other()
    {
        var cells = DatabaseIconBuilder.Build([Layer("A", Color.Red, 10000), Layer("B", Color.Blue, 1)]);

        Assert.Equal(8, cells.Count(c => c == Color.Red));

        Assert.Equal(Color.Blue, cells[8]);
    }

    [Fact]
    public void A_Single_Object_Takes_Every_Cell()
    {
        var cells = DatabaseIconBuilder.Build([Layer("A", Color.Red, 10000)]);

        Assert.All(cells, c => Assert.Equal(Color.Red, c));
    }

    [Fact]
    public void System_And_Overlay_Layers_Take_No_Part()
    {
        var system = Layer("System Objects", Color.Gray, 1000);

        system.IsSystemObject = true;

        var overlay = Layer("GAM", Color.Orange, 5000);

        overlay.IsAllocationLayer = true;

        var cells = DatabaseIconBuilder.Build([system, overlay, Layer("A", Color.Red, 10)]);

        Assert.All(cells, c => Assert.Equal(Color.Red, c));
    }

    [Fact]
    public void A_Database_With_No_Object_Layers_Keeps_The_Logo_Colours()
    {
        var cells = DatabaseIconBuilder.Build([]);

        Assert.Equal(DatabaseIconBuilder.DefaultCells, cells);
    }

    private static AllocationLayer Layer(string name, Color colour, long totalPages)
        => new() { Name = name, Colour = colour, TotalPages = totalPages };
}
