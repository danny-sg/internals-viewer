using Windows.Foundation;
using InternalsViewer.UI.App.Controls.HexView;

namespace InternalsViewer.UI.App.Tests.Controls.HexView;

/// <summary>
/// The shapes drawn over the hex, measured against a monospace font of a known width
/// </summary>
public class HexMetricsTests
{
    private const double CharacterWidth = 10;

    private const double LineHeight = 16;

    private static readonly HexMetrics Metrics = HexMetrics.Measure(t => t.Length * CharacterWidth);

    [Fact]
    public void A_Span_Within_One_Line_Is_One_Rectangle()
    {
        var rects = Metrics.GetSpanRects(2, 4, LineHeight);

        var rect = Assert.Single(rects);

        Assert.True(rect.Left < rect.Right);

        Assert.InRange(rect.Height, LineHeight, LineHeight + 4);
    }

    [Fact]
    public void A_Span_Covers_The_Bytes_It_Names()
    {
        var rect = Assert.Single(Metrics.GetSpanRects(2, 4, LineHeight));

        var byteStart = Metrics.ColumnPositions[2];

        var byteEnd = Metrics.ColumnPositions[4] + Metrics.ByteWidth;

        Assert.True(rect.Left <= byteStart);

        Assert.True(rect.Right >= byteEnd);
    }

    [Fact]
    public void A_Span_Over_Three_Lines_Is_Three_Rectangles()
    {
        Assert.Equal(3, Metrics.GetSpanRects(4, 40, LineHeight).Count);
    }

    [Fact]
    public void A_Span_Over_Two_Lines_Is_Two_Rectangles()
    {
        Assert.Equal(2, Metrics.GetSpanRects(4, 20, LineHeight).Count);
    }

    /// <remarks>
    /// The mask cuts its hole with an even odd fill, which would take an overlap back out of the hole again.
    /// </remarks>
    [Fact]
    public void The_Rectangles_Of_A_Span_Do_Not_Overlap()
    {
        var rects = Metrics.GetSpanRects(4, 40, LineHeight);

        for (var i = 1; i < rects.Count; i++)
        {
            Assert.True(rects[i].Top >= rects[i - 1].Bottom);
        }
    }

    [Fact]
    public void The_Whole_Lines_Of_A_Span_Reach_Both_Edges()
    {
        var rects = Metrics.GetSpanRects(4, 40, LineHeight);

        var edges = Metrics.GetSpanRects(0, HexLayout.BytesPerLine - 1, LineHeight)[0];

        Assert.Equal(edges.Left, rects[1].Left);

        Assert.Equal(edges.Right, rects[1].Right);
    }

    [Fact]
    public void A_Border_Within_One_Line_Is_One_Box()
    {
        var border = Metrics.GetSpanBorder(2, 4, LineHeight);

        Assert.Single(border.Boxes);

        Assert.Empty(border.Outline);
    }

    /// <remarks>
    /// Two lines whose bytes do not overlap horizontally have no outline to draw, so each is boxed on its own.
    /// </remarks>
    [Fact]
    public void Two_Lines_That_Do_Not_Meet_Are_Boxed_Separately()
    {
        var border = Metrics.GetSpanBorder(14, 17, LineHeight);

        Assert.Equal(2, border.Boxes.Count);

        Assert.Empty(border.Outline);
    }

    [Fact]
    public void A_Border_Over_Two_Overlapping_Lines_Is_An_Outline()
    {
        var border = Metrics.GetSpanBorder(2, 20, LineHeight);

        Assert.Empty(border.Boxes);

        Assert.Equal(8, border.Outline.Count);
    }

    [Fact]
    public void A_Border_Over_Three_Lines_Is_An_Outline()
    {
        var border = Metrics.GetSpanBorder(4, 40, LineHeight);

        Assert.Empty(border.Boxes);

        Assert.Equal(8, border.Outline.Count);
    }

    [Fact]
    public void Clamp_Keeps_Only_The_Part_Within_The_Bounds()
    {
        var clamped = HexMetrics.Clamp(new Rect(-5, -5, 20, 20), new Rect(0, 0, 100, 100));

        Assert.Equal(0, clamped.Left);

        Assert.Equal(0, clamped.Top);

        Assert.Equal(15, clamped.Width);
    }

    [Fact]
    public void Clamp_Gives_Nothing_Where_The_Two_Do_Not_Meet()
    {
        var clamped = HexMetrics.Clamp(new Rect(-20, 0, 10, 10), new Rect(0, 0, 100, 100));

        Assert.Equal(0, clamped.Width);

        Assert.Equal(0, clamped.Height);
    }

    /// <remarks>
    /// A cutout is padded past its bytes, so one against the first column reaches outside the mask it is taken from.
    /// </remarks>
    [Fact]
    public void A_Span_At_The_First_Column_Reaches_Past_The_Left_Edge()
    {
        var rect = Assert.Single(Metrics.GetSpanRects(0, 1, LineHeight));

        Assert.True(rect.Left < 0);
    }
}
