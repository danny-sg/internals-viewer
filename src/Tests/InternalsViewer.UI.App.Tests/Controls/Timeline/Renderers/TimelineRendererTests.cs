using InternalsViewer.UI.App.Controls.Timeline;
using InternalsViewer.UI.App.Controls.Timeline.Renderers;
using SkiaSharp;

namespace InternalsViewer.UI.App.Tests.Controls.Timeline.Renderers;

[Trait("Category", "Unit")]
[Trait("Area", "Timeline")]
public class TimelineRendererTests
{
    private static readonly SKColor TickColour = new(110, 110, 110);

    [Fact]
    public void Draws_A_Ruler_Tick_At_Each_Nice_Interval()
    {
        using var resources = new RenderResource();

        using var renderer = new TimelineRenderer(resources);

        using var rowSet = new TimelineRowSet();

        var frame = Frame(rowSet, canvasWidth: 400, rowLabelWidth: 80);

        using var bitmap = new SKBitmap(400, 50);

        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.Black);

        renderer.DrawRuler(canvas, frame);

        var tickColumns = Enumerable.Range(0, 400)
                                    .Where(x => bitmap.GetPixel(x, 15) == TickColour)
                                    .ToList();

        Assert.Equal(4, tickColumns.Count);

        int[] expected = [80, 180, 280, 380];

        foreach (var (column, expectedColumn) in tickColumns.Zip(expected))
        {
            Assert.InRange(column, expectedColumn - 1, expectedColumn + 1);
        }
    }

    [Fact]
    public void Draws_Alternating_Row_Backgrounds()
    {
        using var resources = new RenderResource();

        using var renderer = new TimelineRenderer(resources);

        using var rowSet = new TimelineRowSet();

        var frame = Frame(rowSet, canvasWidth: 400, rowLabelWidth: 80, rowHeight: 8);

        using var bitmap = new SKBitmap(400, 60);

        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.Black);

        renderer.DrawRows(canvas, frame);

        Assert.Equal(frame.LaneColour, bitmap.GetPixel(200, 3));
        Assert.Equal(frame.AlternateLaneColour, bitmap.GetPixel(200, 11));
        Assert.Equal(frame.LaneColour, bitmap.GetPixel(200, 19));
    }

    private static TimelineFrame Frame(TimelineRowSet rowSet,
                                       float canvasWidth,
                                       float rowLabelWidth,
                                       float rowHeight = 10)
    {
        var rowCount = rowSet.Active.Count;

        return new TimelineFrame
        {
            Events = [],
            Times = [],
            Rows = rowSet,
            RowTops = [.. Enumerable.Range(0, rowCount).Select(r => r * rowHeight)],
            RowHeights = [.. Enumerable.Repeat(rowHeight, rowCount)],
            CanvasWidth = canvasWidth,
            RowLabelWidth = rowLabelWidth,
            RowPadding = 0,
            AxisUnitsPerMs = 1000,
            TimeToX = ms => (float)(rowLabelWidth + ms),
            RowMarkerWidth = _ => 1,
            ColourProvider = null,
            ShowThreads = false,
            LaneColour = new SKColor(20, 20, 20),
            AlternateLaneColour = new SKColor(45, 45, 45),
            MinTime = 0,
            XToTime = x => x - rowLabelWidth,
        };
    }
}
