using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Waits;
using InternalsViewer.UI.App.Controls.Timeline;
using InternalsViewer.UI.App.Controls.Timeline.Definition;
using InternalsViewer.UI.App.Controls.Timeline.Renderers;
using InternalsViewer.UI.App.Services.Query.Timeline;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query;
using SkiaSharp;

namespace InternalsViewer.UI.App.Tests.Controls.Timeline.Renderers;

[Trait("Category", "Unit")]
[Trait("Area", "Timeline")]
public class MarkerRendererTests
{
    private const float BandHeight = 20;

    private const float BandPadding = 2;

    private const float BandLabelWidth = 50;

    private static readonly TimelineBandVisibility ShowAll = new(ShowLocks: true, ShowLatches: true, ShowWaits: true);

    private static readonly SKColor ReadColour = ColourConstants.IoColour.ToSkColor().WithAlpha(255);

    private static readonly SKColor WaitColour = ColourConstants.WaitColour.ToSkColor().WithAlpha(255);

    private static readonly SKColor SegmentColour = ColourConstants.SegmentColour.ToSkColor().WithAlpha(255);

    [Fact]
    public void Draws_A_Cached_Read_In_The_Top_Half_And_A_Disk_Read_In_The_Bottom_Half()
    {
        using var render = Render(
        [
            new ReadEventGroup { Events = [], ReadType = ReadType.Cached },
            new ReadEventGroup { Events = [], ReadType = ReadType.NonCached },
        ],
        [10, 20]);

        var top = render.BandTop(typeof(ReadEventGroup)) + BandPadding;

        Assert.Equal(ReadColour, render.Pixel(60, top + 3));
        Assert.Equal(SKColors.Black, render.Pixel(60, top + 11));
        Assert.Equal(SKColors.Black, render.Pixel(70, top + 3));
        Assert.Equal(ReadColour, render.Pixel(70, top + 11));
    }

    [Fact]
    public void Ticks_A_Read_With_Duration_At_Its_End_Over_A_Translucent_Bar()
    {
        using var render = Render([new ReadEventGroup { Events = [], ReadType = ReadType.Cached, DurationUs = 20_000 }], [10]);

        var y = render.BandTop(typeof(ReadEventGroup)) + BandPadding + 3;

        Assert.Equal(ReadColour, render.Pixel(79, y));

        var bar = render.Pixel(65, y);

        Assert.NotEqual(ReadColour, bar);
        Assert.NotEqual(SKColors.Black, bar);
    }

    [Fact]
    public void Steps_A_Wait_By_Its_Category_In_A_Category_Tint()
    {
        using var render = Render(
        [
            new WaitEvent { Category = EventCategory.Io },
            new WaitEvent { Category = EventCategory.Parallelism },
        ],
        [10, 20]);

        var top = render.BandTop(typeof(WaitEvent)) + BandPadding;

        Assert.Equal(TimelineColours.TintByCategory(WaitColour, 0), render.Pixel(60, top + 1));
        Assert.Equal(SKColors.Black, render.Pixel(60, top + 13));
        Assert.Equal(TimelineColours.TintByCategory(WaitColour, 3), render.Pixel(70, top + 13));
    }

    [Fact]
    public void Stacks_Overlapping_Segment_Scans_As_Solid_Bars_In_The_Top_Half()
    {
        using var render = Render(
        [
            new SegmentScanEvent { RowGroupId = 0, ColumnId = 1, TimeUs = 10_000, DurationUs = 20_000 },
            new SegmentScanEvent { RowGroupId = 0, ColumnId = 2, TimeUs = 15_000, DurationUs = 20_000 },
        ],
        [10, 15]);

        var top = render.BandTop(typeof(SegmentScanEvent)) + BandPadding;

        Assert.Equal(SegmentColour, render.Pixel(70, top + 1));
        Assert.Equal(SKColors.Black, render.Pixel(70, top + 3));
        Assert.Equal(SegmentColour, render.Pixel(70, top + 5));
        Assert.Equal(SKColors.Black, render.Pixel(70, top + 12));
    }

    [Fact]
    public void Draws_An_Object_Pool_Hit_In_The_Bottom_Half_At_Least_Four_Pixels_Wide()
    {
        using var render = Render([new ObjectPoolEvent { IsHit = true }], [10]);

        var top = render.BandTop(typeof(SegmentScanEvent)) + BandPadding;

        var hit = ColourConstants.ObjectPoolHitColour.ToSkColor().WithAlpha(255);

        Assert.Equal(hit, render.Pixel(60, top + 12));
        Assert.Equal(hit, render.Pixel(63, top + 12));
        Assert.Equal(SKColors.Black, render.Pixel(64, top + 12));
        Assert.Equal(SKColors.Black, render.Pixel(60, top + 4));
    }

    [Fact]
    public void Draws_An_Object_Pool_Hit_Over_A_Miss_On_The_Same_Track()
    {
        var hit = new ObjectPoolEvent { IsHit = true, RowGroupId = 0, ColumnId = 2 };

        var miss = new ObjectPoolEvent { IsHit = false, RowGroupId = 0, ColumnId = 2, DurationUs = 30_000 };

        using var render = Render([hit, miss], [20, 10]);

        var top = render.BandTop(typeof(SegmentScanEvent)) + BandPadding;

        Assert.Equal(ColourConstants.ObjectPoolHitColour.ToSkColor().WithAlpha(255), render.Pixel(70, top + 12));
        Assert.Equal(ColourConstants.ObjectPoolMissColour.ToSkColor().WithAlpha(255), render.Pixel(80, top + 12));
    }

    private static Rendered Render(EngineEvent[] events, double[] timesMs)
    {
        var resources = new RenderResource();

        var bands = new TimelineBandSet();

        var definition = TimelineDefinitionBuilder.CreateDefault().Build(events, ShowAll);

        bands.Rebuild(definition, resources.LabelFont);

        var bandCount = bands.Active.Count;

        var frame = new TimelineFrame
        {
            Times = timesMs,
            Bands = bands,
            Definition = definition,
            BandTops = [.. Enumerable.Range(0, bandCount).Select(r => r * BandHeight)],
            BandHeights = [.. Enumerable.Repeat(BandHeight, bandCount)],
            CanvasWidth = 200,
            BandLabelWidth = BandLabelWidth,
            BandPadding = BandPadding,
            AxisUnitsPerMs = 1000,
            TimeToX = ms => (float)(BandLabelWidth + ms),
            BandMarkerWidth = _ => 1,
            ColourProvider = null,
            ShowThreads = false,
            BandColour = SKColors.Black,
            AlternateBandColour = SKColors.Black,
            MinTime = 0,
            XToTime = x => x - BandLabelWidth,
        };

        var bitmap = new SKBitmap(200, (int)(bandCount * BandHeight));

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Black);

            new MarkerRenderer(resources, new CurrentSelection(), []).Draw(canvas, frame);
        }

        return new Rendered(bitmap, bands, resources);
    }

    private sealed class Rendered(SKBitmap bitmap, TimelineBandSet bands, RenderResource resources) : IDisposable
    {
        public float BandTop(Type key) => bands.IndexOf(key) * BandHeight;

        public SKColor Pixel(float x, float y) => bitmap.GetPixel((int)x, (int)y);

        public void Dispose()
        {
            bitmap.Dispose();

            bands.Dispose();

            resources.Dispose();
        }
    }
}
