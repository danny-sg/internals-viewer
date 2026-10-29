using InternalsViewer.UI.App.Controls.Query.Trace.Timeline;
using SkiaSharp;

namespace InternalsViewer.UI.App.Tests.Controls.Query.Trace.Timeline;

public class TraceIndicatorLayoutTests
{
    private static readonly SKRect Area = new(0, 100, 800, 500);

    [Fact]
    public void The_Callout_Goes_Above_The_Targets_When_There_Is_Room()
    {
        SKPoint[] anchors = [new SKPoint(300, 400), new SKPoint(500, 300)];

        var (callout, isAbove) = TraceIndicatorLayout.Place(new SKSize(200, 60), anchors, 20, Area, 8, 28);

        Assert.True(isAbove);
        Assert.Equal(300 - 28 - 60, callout.Top);
        Assert.Equal(400 - 100, callout.Left);
    }

    [Fact]
    public void The_Callout_Goes_Below_Targets_Near_The_Top()
    {
        var (callout, isAbove) = TraceIndicatorLayout.Place(new SKSize(200, 60), [new SKPoint(400, 120)], 20, Area, 8, 28);

        Assert.False(isAbove);
        Assert.Equal(120 + 20 + 28, callout.Top);
    }

    [Fact]
    public void The_Callout_Stays_Inside_The_Pane()
    {
        var (callout, _) = TraceIndicatorLayout.Place(new SKSize(200, 60), [new SKPoint(790, 400)], 20, Area, 8, 28);

        Assert.Equal(800 - 200 - 8, callout.Left);
    }

    [Fact]
    public void A_Dragged_Callout_Is_Kept_Inside_The_Pane()
    {
        var kept = TraceIndicatorLayout.Keep(new SKPoint(750, 50), new SKSize(200, 60), Area);

        Assert.Equal((600f, 100f), (kept.Left, kept.Top));
    }

    [Fact]
    public void An_Arrow_Leaves_The_Side_Of_The_Callout_Facing_Its_Call()
    {
        var callout = SKRect.Create(300, 200, 200, 60);

        var below = TraceIndicatorLayout.GetArrow(callout, new SKPoint(400, 300), 320, 8);

        var above = TraceIndicatorLayout.GetArrow(callout, new SKPoint(400, 120), 140, 8);

        var left = TraceIndicatorLayout.GetArrow(callout, new SKPoint(100, 220), 240, 8);

        Assert.Equal((new SKPoint(400, 260), new SKPoint(400, 300)), below);
        Assert.Equal((new SKPoint(400, 200), new SKPoint(400, 140)), above);
        Assert.Equal((new SKPoint(300, 230), new SKPoint(100, 230)), left);
    }

    [Fact]
    public void A_Call_Under_The_Callout_Gets_No_Arrow()
        => Assert.Null(TraceIndicatorLayout.GetArrow(SKRect.Create(300, 200, 200, 60), new SKPoint(400, 210), 230, 8));

    [Fact]
    public void The_Callout_Sits_In_The_Middle_Of_The_Pane_By_Default()
    {
        var callout = TraceIndicatorLayout.GetDefault(new SKSize(200, 60), [(new SKPoint(100, 450), 470f)], 20, Area, 8, 28);

        Assert.Equal((300f, 270f), (callout.Left, callout.Top));
    }

    [Fact]
    public void The_Callout_Moves_Off_A_Call_It_Would_Cover_In_The_Middle()
    {
        var callout = TraceIndicatorLayout.GetDefault(new SKSize(200, 60), [(new SKPoint(400, 290), 310f)], 20, Area, 8, 28);

        Assert.False(TraceIndicatorLayout.Covers(callout, new SKPoint(400, 290), 310));
        Assert.Equal(290 - 28 - 60, callout.Top);
    }
}
