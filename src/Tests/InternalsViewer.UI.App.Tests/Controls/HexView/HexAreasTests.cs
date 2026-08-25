using InternalsViewer.UI.App.Controls.HexView;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.Tests.Controls.HexView;

public class HexAreasTests
{
    private static readonly HexArea[] Areas =
    [
        new("Header", 0),
        new("Handles", 64),
        new("Pages", 256)
    ];

    [Theory]
    [InlineData(0, "Header")]
    [InlineData(63, "Header")]
    [InlineData(64, "Handles")]
    [InlineData(255, "Handles")]
    [InlineData(4096, "Pages")]
    public void NameAt_Takes_The_Last_Area_Starting_At_Or_Before_The_Offset(int offset, string expected)
    {
        Assert.Equal(expected, HexAreas.NameAt(Areas, offset));
    }

    [Fact]
    public void NameAt_Gives_Nothing_Before_The_First_Area()
    {
        Assert.Equal(string.Empty, HexAreas.NameAt([new HexArea("Pages", 256)], 0));
    }

    [Fact]
    public void A_Name_Is_Written_Once_Where_Its_Area_Starts()
    {
        var labels = HexAreas.GetLabels(Areas, 0, 32).ToList();

        Assert.Equal(["Header", "Handles", "Pages"], labels.Select(l => l.Name));

        Assert.Equal([0, 4, 16], labels.Select(l => l.Line));
    }

    /// <remarks>
    /// The window is where the reader meets an area, so one running in from above still names the first line.
    /// </remarks>
    [Fact]
    public void An_Area_Starting_Before_The_Window_Names_Its_First_Line()
    {
        var labels = HexAreas.GetLabels(Areas, 128, 4).ToList();

        var label = Assert.Single(labels);

        Assert.Equal("Handles", label.Name);

        Assert.Equal(0, label.Line);
    }

    [Fact]
    public void No_Areas_Gives_No_Labels()
    {
        Assert.Empty(HexAreas.GetLabels([], 0, 32));
    }
}
