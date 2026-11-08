using System.Xml.Linq;
using InternalsViewer.UI.App.Controls.Query.Plan;

namespace InternalsViewer.UI.App.Tests.Controls.Query.Plan;

[Trait("Category", "Unit")]
public class PlanIconPartsTests
{
    private const string Red = "#FF0000";

    private const string Icon = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
          <g stroke="#424242" fill="none" data-part="Branches"><path d="M 16 5 L 16 25"/></g>
          <g fill="#0078D4">
            <circle cx="16" cy="5" r="2" data-part="Root Node"/>
            <circle cx="16" cy="13" r="2" display="none" data-part="Intermediate Node"/>
          </g>
          <path d="M 2 2 L 4 4" stroke="#0078D4" fill="none" data-part="Arrow"/>
        </svg>
        """;

    [Fact]
    public void Parts_Are_Listed_In_Drawing_Order()
    {
        Assert.Equal(["Branches", "Root Node", "Intermediate Node", "Arrow"], PlanIconParts.GetParts(XDocument.Parse(Icon)));
    }

    [Fact]
    public void An_Overlay_Paints_The_Part_And_Keeps_What_Is_Drawn_Over_It()
    {
        var shapes = GetShapes("Root Node");

        Assert.Equal(["Root Node", "Intermediate Node", "Arrow"], shapes.Select(s => (string?)s.Attribute("data-part")));

        var painted = ((string?)shapes[0].Attribute("fill"),
                       (string?)shapes[1].Attribute("display"),
                       (string?)shapes[2].Attribute("stroke"));

        Assert.Equal((Red, "none", "#0078D4"), painted);
    }

    [Fact]
    public void An_Overlay_Shows_A_Hidden_Part()
    {
        var circle = GetShapes("Intermediate Node")[0];

        Assert.Equal((null, Red), ((string?)circle.Attribute("display"), (string?)circle.Attribute("fill")));
    }

    [Fact]
    public void A_Stroked_Part_Is_Painted_Without_A_Fill()
    {
        var path = GetShapes("Branches")[0];

        Assert.Equal((Red, null), ((string?)path.Attribute("stroke"), (string?)path.Attribute("fill")));
    }

    [Theory]
    [InlineData(0, "14.5")]
    [InlineData(0.5, "16.5")]
    [InlineData(1, "18.5")]
    public void A_Ranged_Part_Is_Placed_By_Its_Fraction_On_Whole_Pixels(double fraction, string expected)
    {
        const string icon = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="32 0 16 32">
              <line data-part="Page Row" data-range="14.5 18.5" x1="36" y1="14.5" x2="44" y2="14.5" stroke="#808080"/>
            </svg>
            """;

        var overlay = XDocument.Parse(PlanIconParts.CreateOverlay(XDocument.Parse(icon), "Page Row", Red, fraction)!);

        var line = overlay.Descendants().Single(e => e.Name.LocalName == "line");

        Assert.Equal((expected, expected), ((string?)line.Attribute("y1"), (string?)line.Attribute("y2")));
    }

    [Fact]
    public void An_Icon_Without_The_Part_Has_No_Overlay()
    {
        Assert.Null(PlanIconParts.CreateOverlay(XDocument.Parse(Icon), "Leaf Nodes", Red));
    }

    [Theory]
    [InlineData("IndexSeek")]
    [InlineData("ClusteredIndexSeek")]
    [InlineData("ClusteredIndexSeekReverse")]
    [InlineData("IndexScan")]
    [InlineData("ClusteredIndexScan")]
    [InlineData("IndexInsert")]
    [InlineData("ClusteredIndexDelete")]
    public void A_B_Tree_Icon_Has_A_Part_For_Each_Level(string icon)
    {
        var levels = (PlanIconParts.GetParts(icon, "Root Node").Count,
                      PlanIconParts.GetParts(icon, "Intermediate Node").Count,
                      PlanIconParts.GetParts(icon, "Leaf Node").Count > 1);

        Assert.Equal((1, 1, true), levels);
    }

    [Theory]
    [InlineData("IndexSeek")]
    [InlineData("ClusteredIndexScan")]
    [InlineData("TableScan")]
    [InlineData("KeyLookup")]
    [InlineData("RIDLookup")]
    public void A_Scan_Seek_Or_Lookup_Icon_Draws_A_Page_With_Its_Parts(string icon)
    {
        Assert.Equal(["Page"], PlanIconParts.HasPage(icon) ? PlanIconParts.GetParts(PlanIconParts.PageIcon, "Page", owner: icon) : []);
    }

    [Fact]
    public void An_Operator_Without_Pages_Draws_No_Page()
    {
        Assert.False(PlanIconParts.HasPage("Top"));
    }

    [Fact]
    public void A_Page_Part_Named_With_An_Operator_Only_Binds_To_That_Operators_Page()
    {
        const string binding = "Table Scan.Page";

        var counts = (PlanIconParts.GetParts(PlanIconParts.PageIcon, binding, owner: "TableScan").Count,
                      PlanIconParts.GetParts(PlanIconParts.PageIcon, binding, owner: "IndexSeek").Count);

        Assert.Equal((1, 0), counts);
    }

    [Fact]
    public void Active_Binds_To_Every_Operator_Icon_But_Not_The_Page()
    {
        var parts = (PlanIconParts.GetParts("Top", "Active").Count,
                     PlanIconParts.GetParts(PlanIconParts.PageIcon, "Active", owner: "TableScan").Count);

        Assert.Equal((1, 0), parts);
    }

    [Fact]
    public void An_Inactive_Icon_Turns_Every_Colour_Grey_And_Keeps_None()
    {
        const string icon = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
              <rect stroke="#424242" fill="none"/>
              <path fill="#0078D4" style="fill:#d40000"/>
            </svg>
            """;

        var inactive = XDocument.Parse(PlanIconParts.CreateInactive(XDocument.Parse(icon)));

        var colours = inactive.Descendants()
                              .SelectMany(e => e.Attributes())
                              .Where(a => a.Name.LocalName is "fill" or "stroke" or "style")
                              .Select(a => a.Value);

        Assert.Equal(["#A0A0A0", "none", "#AFAFAF", "fill:#9F9F9F"], colours);
    }

    [Fact]
    public void Every_Operator_Icon_Is_Square()
    {
        var sizes = Directory.EnumerateFiles(PlanIconResolver.IconFolder, "*.svg")
                             .Where(p => Path.GetFileNameWithoutExtension(p) != PlanIconParts.PageIcon)
                             .Select(XDocument.Load)
                             .Select(d => ((string?)d.Root!.Attribute("viewBox"))?.Split(' ') is [_, _, var width, var height]
                                              ? (width, height)
                                              : (string.Empty, string.Empty))
                             .Distinct();

        Assert.Equal([("32", "32")], sizes);
    }

    [Fact]
    public void A_Kind_Without_A_Number_Binds_To_Every_Part_Of_That_Kind()
    {
        Assert.Equal(["Leaf Node 1", "Leaf Node 2", "Leaf Node 3"], PlanIconParts.GetParts("IndexScan", "Leaf Node"));
    }

    [Fact]
    public void A_Numbered_Part_Binds_To_That_Part_Alone()
    {
        Assert.Equal(["Leaf Node 2"], PlanIconParts.GetParts("IndexScan", "Leaf Node 2"));
    }

    [Fact]
    public void A_Part_Named_With_An_Operator_Only_Binds_To_That_Operator()
    {
        const string binding = "Index Seek.Root Node";

        Assert.Equal((1, 0), (PlanIconParts.GetParts("IndexSeek", binding).Count, PlanIconParts.GetParts("IndexScan", binding).Count));
    }

    private static List<XElement> GetShapes(string part)
    {
        var overlay = XDocument.Parse(PlanIconParts.CreateOverlay(XDocument.Parse(Icon), part, Red)!);

        return [.. overlay.Descendants().Where(e => e.Name.LocalName is "path" or "circle")];
    }
}
