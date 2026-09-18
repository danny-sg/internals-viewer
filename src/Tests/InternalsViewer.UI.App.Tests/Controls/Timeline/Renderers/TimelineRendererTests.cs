using InternalsViewer.UI.App.Controls.Timeline;
using InternalsViewer.UI.App.Controls.Timeline.Definition;
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

        using var bandSet = new TimelineBandSet();

        var frame = Frame(bandSet, canvasWidth: 400, bandLabelWidth: 80);

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
    public void Draws_Alternating_Band_Backgrounds()
    {
        using var resources = new RenderResource();

        using var renderer = new TimelineRenderer(resources);

        using var bandSet = new TimelineBandSet();

        var definition = new TimelineDefinition([],
        [
            new TimelineBand(typeof(object), "First", SKColors.White, 1f),
            new TimelineBand(typeof(object), "Second", SKColors.White, 1f),
            new TimelineBand(typeof(object), "Third", SKColors.White, 1f),
        ],
        [],
        []);

        bandSet.Rebuild(definition, resources.LabelFont);

        var frame = Frame(bandSet, canvasWidth: 400, bandLabelWidth: 80, bandHeight: 8, definition);

        using var bitmap = new SKBitmap(400, 60);

        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.Black);

        renderer.DrawBands(canvas, frame);

        Assert.Equal(frame.BandColour, bitmap.GetPixel(200, 3));
        Assert.Equal(frame.AlternateBandColour, bitmap.GetPixel(200, 11));
        Assert.Equal(frame.BandColour, bitmap.GetPixel(200, 19));
    }

    [Fact]
    public void Draws_A_Divider_Above_Each_Lane_In_A_Band()
    {
        using var resources = new RenderResource();

        using var renderer = new TimelineRenderer(resources);

        using var bandSet = new TimelineBandSet();

        var definition = new TimelineDefinition([],
        [
            new TimelineBand(typeof(object), "C", SKColors.White, 1f)
            {
                TrackDividers = [new TimelineTrackDivider(2, 4)],
            },
        ],
        [],
        []);

        bandSet.Rebuild(definition, resources.LabelFont);

        var frame = Frame(bandSet, canvasWidth: 400, bandLabelWidth: 80, bandHeight: 40, definition);

        using var bitmap = new SKBitmap(400, 40);

        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.Black);

        renderer.DrawBands(canvas, frame);

        Assert.NotEqual(frame.BandColour, bitmap.GetPixel(200, 19));
        Assert.Equal(frame.BandColour, bitmap.GetPixel(200, 21));
        Assert.Equal(frame.BandColour, bitmap.GetPixel(40, 19));
    }

    [Fact]
    public void Fills_The_Band_Area_With_The_Band_Colour_When_There_Are_No_Bands()
    {
        using var resources = new RenderResource();

        using var renderer = new TimelineRenderer(resources);

        using var bandSet = new TimelineBandSet();

        var frame = Frame(bandSet, canvasWidth: 400, bandLabelWidth: 80);

        using var bitmap = new SKBitmap(400, 60);

        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.Black);

        renderer.DrawEmpty(canvas, frame, top: 20, height: 40);

        Assert.Equal(SKColors.Black, bitmap.GetPixel(200, 10));
        Assert.Equal(frame.BandColour, bitmap.GetPixel(200, 30));
        Assert.Equal(frame.BandColour, bitmap.GetPixel(10, 59));
    }

    private static TimelineFrame Frame(TimelineBandSet bandSet,
                                       float canvasWidth,
                                       float bandLabelWidth,
                                       float bandHeight = 10,
                                       TimelineDefinition? definition = null)
    {
        var bandCount = bandSet.Active.Count;

        return new TimelineFrame
        {
            Times = [],
            Bands = bandSet,
            Definition = definition ?? TimelineDefinition.Empty,
            BandTops = [.. Enumerable.Range(0, bandCount).Select(r => r * bandHeight)],
            BandHeights = [.. Enumerable.Repeat(bandHeight, bandCount)],
            CanvasWidth = canvasWidth,
            BandLabelWidth = bandLabelWidth,
            BandPadding = 0,
            AxisUnitsPerMs = 1000,
            TimeToX = ms => (float)(bandLabelWidth + ms),
            BandMarkerWidth = _ => 1,
            ColourProvider = null,
            ShowThreads = false,
            BandColour = new SKColor(20, 20, 20),
            AlternateBandColour = new SKColor(45, 45, 45),
            MinTime = 0,
            XToTime = x => x - bandLabelWidth,
        };
    }
}
