using Windows.Foundation;
using InternalsViewer.UI.App.Controls.HexView;

namespace InternalsViewer.UI.App.Tests.Controls.HexView;

[Trait("Category", "Unit")]
public class HexTipLayoutTests
{
    private const double CharacterWidth = 10;

    private const double LineHeight = 16;

    private const int Length = 512;

    private static readonly HexMetrics Metrics = HexMetrics.Measure(t => t.Length * CharacterWidth);

    private static readonly Rect View = new(0, 0, 600, 400);

    [Fact]
    public void The_Anchor_Is_The_Byte_The_Tip_Describes()
    {
        var bounds = Metrics.GetSpanRects(18, 18, LineHeight)[0];

        Assert.Equal(bounds, GetAnchor(18, View));
    }

    [Fact]
    public void A_Tip_Opens_Below_Its_Line_And_A_Value_Sits_Beside_Its_Byte()
    {
        var anchor = GetAnchor(18, View);

        Assert.NotNull(anchor);

        var tip = HexTipLayout.GetTipPosition(anchor.Value);

        var value = HexTipLayout.GetValuePosition(anchor.Value, 0);

        Assert.True(tip.X >= anchor.Value.Right);

        Assert.True(tip.Y >= anchor.Value.Bottom);

        Assert.True(value.X >= anchor.Value.Right);

        Assert.Equal(anchor.Value.Top, value.Y);
    }

    [Fact]
    public void Values_On_The_Same_Byte_Sit_Side_By_Side()
    {
        var anchor = GetAnchor(18, View);

        Assert.NotNull(anchor);

        var first = HexTipLayout.GetValuePosition(anchor.Value, 0);

        var second = HexTipLayout.GetValuePosition(anchor.Value, 50);

        Assert.Equal(first.X + 50, second.X);

        Assert.Equal(first.Y, second.Y);
    }

    [Fact]
    public void Scrolling_Moves_The_Anchor_With_Its_Byte()
    {
        var before = GetAnchor(100, View);

        var after = GetAnchor(100, Scroll(View, 0, 3 * LineHeight));

        Assert.NotNull(before);

        Assert.NotNull(after);

        Assert.Equal(before.Value.X, after.Value.X);

        Assert.Equal(before.Value.Y - (3 * LineHeight), after.Value.Y);
    }

    [Fact]
    public void A_Byte_Scrolled_Above_The_View_Has_No_Anchor()
    {
        Assert.Null(GetAnchor(18, Scroll(View, 0, 5 * LineHeight)));
    }

    [Fact]
    public void A_Byte_Below_The_View_Has_No_Anchor()
    {
        Assert.Null(GetAnchor(Length - 1, View));
    }

    [Fact]
    public void An_Anchor_Hidden_By_Scrolling_Returns_To_The_Same_Place()
    {
        var before = GetAnchor(18, View);

        var hidden = GetAnchor(18, Scroll(View, 0, 5 * LineHeight));

        var after = GetAnchor(18, View);

        Assert.Null(hidden);

        Assert.Equal(before, after);
    }

    [Fact]
    public void The_Anchor_Goes_Once_Most_Of_Its_Byte_Has_Left_The_View()
    {
        var line = 2 * LineHeight;

        Assert.NotNull(GetAnchor(32, new Rect(0, 0, 600, line + (LineHeight * 0.75))));

        Assert.Null(GetAnchor(32, new Rect(0, 0, 600, line + (LineHeight * 0.25))));
    }

    [Fact]
    public void Shrinking_The_View_Past_The_Byte_Hides_The_Anchor_Until_It_Grows_Again()
    {
        var before = GetAnchor(160, View);

        var shrunk = GetAnchor(160, new Rect(0, 0, 600, 100));

        var grown = GetAnchor(160, View);

        Assert.NotNull(before);

        Assert.Null(shrunk);

        Assert.Equal(before, grown);
    }

    [Fact]
    public void A_Byte_Cut_Off_At_The_Right_Has_No_Anchor()
    {
        Assert.NotNull(GetAnchor(15, View));

        Assert.Null(GetAnchor(15, new Rect(0, 0, 200, 400)));
    }

    [Fact]
    public void The_Anchor_Follows_The_Text_Past_The_Address_Column()
    {
        var atEdge = GetAnchor(18, View);

        var indented = GetAnchor(18, new Rect(-80, 0, 600, 400));

        Assert.NotNull(atEdge);

        Assert.NotNull(indented);

        Assert.Equal(atEdge.Value.X + 80, indented.Value.X);

        Assert.Equal(atEdge.Value.Y, indented.Value.Y);
    }

    [Fact]
    public void A_Byte_Outside_The_Window_Has_No_Anchor()
    {
        Assert.Null(HexTipLayout.GetAnchor(Metrics, 100, 160, 64, LineHeight, View));

        Assert.Null(HexTipLayout.GetAnchor(Metrics, 224, 160, 64, LineHeight, View));
    }

    [Fact]
    public void Moving_The_Window_Moves_The_Anchor_By_Whole_Lines()
    {
        var before = HexTipLayout.GetAnchor(Metrics, 200, 160, 64, LineHeight, View);

        var after = HexTipLayout.GetAnchor(Metrics, 200, 176, 64, LineHeight, View);

        Assert.NotNull(before);

        Assert.NotNull(after);

        Assert.Equal(before.Value.X, after.Value.X);

        Assert.Equal(before.Value.Y - LineHeight, after.Value.Y);
    }

    [Fact]
    public void A_Byte_Moved_Out_Of_The_Window_Comes_Back_With_It()
    {
        var before = HexTipLayout.GetAnchor(Metrics, 200, 160, 64, LineHeight, View);

        var moved = HexTipLayout.GetAnchor(Metrics, 200, 320, 64, LineHeight, View);

        var after = HexTipLayout.GetAnchor(Metrics, 200, 160, 64, LineHeight, View);

        Assert.Null(moved);

        Assert.Equal(before, after);
    }

    [Fact]
    public void The_Pin_Button_Offers_Unpin_Once_Pinned()
    {
        Assert.Equal("Pin", HexTipPin.GetLabel(false));

        Assert.Equal("", HexTipPin.GetGlyph(false));

        Assert.Equal("Unpin", HexTipPin.GetLabel(true));

        Assert.Equal("", HexTipPin.GetGlyph(true));
    }

    private static Rect? GetAnchor(int anchor, Rect viewport)
        => HexTipLayout.GetAnchor(Metrics, anchor, 0, Length, LineHeight, viewport);

    private static Rect Scroll(Rect viewport, double x, double y)
        => new(viewport.X + x, viewport.Y + y, viewport.Width, viewport.Height);
}
