using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.UI.App.Controls.Timeline;
using InternalsViewer.UI.App.Controls.Timeline.Definition;
using SkiaSharp;

namespace InternalsViewer.UI.App.Tests.Controls.Timeline;

[Trait("Category", "Unit")]
[Trait("Area", "Timeline")]
public class TimelineBandLayoutTests
{
    private static readonly TimelineBand[] Bands =
    [
        new(typeof(EngineEvent), "Plan", SKColors.White, 3f),
        new(typeof(SegmentScanEvent), "Segment Scan", SKColors.White, 1f),
        new(typeof(ReadEventGroup), "Read", SKColors.White, 1f),
    ];

    [Fact]
    public void Splits_The_Height_By_Weight_When_The_Held_Band_Already_Fits()
    {
        var heights = TimelineBandLayout.Resolve(Bands, height: 100, heldBand: 1, heldMinHeight: 10);

        Assert.Equal(new[] { 60f, 20f, 20f }, heights);
    }

    [Fact]
    public void Holds_The_Band_At_Its_Minimum_And_Shares_The_Rest_By_Weight()
    {
        var heights = TimelineBandLayout.Resolve(Bands, height: 100, heldBand: 1, heldMinHeight: 40);

        Assert.Equal(40f, heights[1]);
        Assert.Equal(45f, heights[0]);
        Assert.Equal(15f, heights[2]);
        Assert.Equal(100f, heights.Sum());
    }

    [Fact]
    public void Gives_Up_The_Minimum_When_It_Would_Swallow_The_Band()
    {
        var heights = TimelineBandLayout.Resolve(Bands, height: 100, heldBand: 1, heldMinHeight: 100);

        Assert.Equal(new[] { 60f, 20f, 20f }, heights);
    }

    [Fact]
    public void Ignores_A_Held_Band_That_Is_Not_Shown()
    {
        var heights = TimelineBandLayout.Resolve(Bands, height: 100, heldBand: -1, heldMinHeight: 40);

        Assert.Equal(new[] { 60f, 20f, 20f }, heights);
    }
}
