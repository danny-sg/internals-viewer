using InternalsViewer.UI.App.Controls.Query.Timeline;
using InternalsViewer.UI.App.Models.Query.Timeline;
using InternalsViewer.UI.App.Models.Query.Timeline.Enums;
using SkiaSharp;

namespace InternalsViewer.UI.App.Tests.Controls.Query.Timeline;

[Trait("Category", "Unit")]
[Trait("Area", "Timeline")]
public class TimelineRulerLayoutTests
{
    private const double Gap = 13;

    private const double EdgeZone = 24;

    private const double EdgePixels = 16;

    private static readonly TimelineRulerArea Area = new(50, 450);

    [Fact]
    public void A_Value_Maps_Into_The_Area_And_Back()
    {
        var state = State(viewStart: 100, viewEnd: 300);

        Assert.Equal((250d, 200d), (TimelineRulerLayout.GetX(state, Area, 200), TimelineRulerLayout.GetValue(state, Area, 250)));
    }

    [Fact]
    public void A_Point_Before_The_Run_Reads_As_Its_Start() => Assert.Equal(100, TimelineRulerLayout.GetValue(State(100, 300), Area, -500));

    [Fact]
    public void The_Crop_Handles_Hug_The_Playhead_Until_It_Is_Cropped()
    {
        var state = State(100, 300, playhead: 200);

        Assert.Equal((250 - Gap, 250 + Gap), TimelineRulerLayout.GetHandleXs(state, Area, Gap));
        Assert.Equal((150d, 350d), TimelineRulerLayout.GetHandleXs(state with { CropStart = 150, CropEnd = 250 }, Area, Gap));
    }

    [Fact]
    public void There_Are_No_Handles_Without_A_Playhead()
        => Assert.Equal((null, null), TimelineRulerLayout.GetHandleXs(State(100, 300), Area, Gap));

    [Fact]
    public void Ticks_Fall_On_Steps_From_The_Origin_Inside_The_View()
    {
        var (step, values) = TimelineRulerLayout.GetTicks(State(120, 300), Area, 110);

        Assert.Equal(50, step);
        Assert.Equal([150d, 200d, 250d, 300d], values);
    }

    [Fact]
    public void Ticks_Stop_At_The_End_Of_The_Run()
        => Assert.Equal(400, TimelineRulerLayout.GetTicks(State(300, 500), Area, 110).Values[^1]);

    [Fact]
    public void Dragging_Away_From_The_Edges_Does_Not_Scroll()
        => Assert.Equal(0, TimelineRulerLayout.GetEdgeStep(State(150, 350), Area, 250, EdgeZone, EdgePixels));

    [Fact]
    public void Dragging_Into_The_Right_Edge_Scrolls_Faster_The_Deeper_It_Goes()
    {
        var state = State(150, 350);

        Assert.Equal((4d, 8d), (TimelineRulerLayout.GetEdgeStep(state, Area, 438, EdgeZone, EdgePixels),
                                TimelineRulerLayout.GetEdgeStep(state, Area, 470, EdgeZone, EdgePixels)));
    }

    [Fact]
    public void Dragging_Into_The_Left_Edge_Scrolls_Back_Only_When_The_View_Has_Room()
        => Assert.Equal((-8d, 0d), (TimelineRulerLayout.GetEdgeStep(State(150, 350), Area, 40, EdgeZone, EdgePixels),
                                    TimelineRulerLayout.GetEdgeStep(State(100, 300), Area, 40, EdgeZone, EdgePixels)));

    [Fact]
    public void Nothing_Scrolls_Past_The_End_Of_The_Run()
        => Assert.Equal(0, TimelineRulerLayout.GetEdgeStep(State(200, 400), Area, 470, EdgeZone, EdgePixels));

    [Fact]
    public void A_Pixel_Width_Converts_To_A_Length_On_The_View()
        => Assert.Equal(50, TimelineRulerLayout.GetLength(State(100, 300), Area, 100));

    [Fact]
    public void Only_A_Mark_Under_The_Pointer_Is_Hit()
    {
        var marks = Marks([100, 200], [110, 205]);

        Assert.Equal((true, false), (TimelineRulerLayout.IsOverMark(marks, 105, 106, 0),
                                     TimelineRulerLayout.IsOverMark(marks, 150, 160, 0)));
    }

    [Fact]
    public void A_Mark_Narrower_Than_The_Minimum_Is_Hit_Across_The_Minimum()
    {
        var marks = Marks([100], [100]);

        Assert.Equal((true, false), (TimelineRulerLayout.IsOverMark(marks, 101, 101, 2),
                                     TimelineRulerLayout.IsOverMark(marks, 103, 103, 2)));
    }

    private static TimelineRulerMarks Marks(double[] starts, double[] ends)
        => new(TimelineRulerMarkType.SearchResult, "Search Results", starts, ends, SKColors.Red, 1);

    private static TimelineRulerState State(double viewStart, double viewEnd, double? playhead = null)
        => new(viewStart, viewEnd, 100, 400, 100, 0, 0, playhead, null, null);
}
