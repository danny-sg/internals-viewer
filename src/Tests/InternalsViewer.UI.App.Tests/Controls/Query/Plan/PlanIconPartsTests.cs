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
