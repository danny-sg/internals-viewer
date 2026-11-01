using InternalsViewer.UI.App.Controls;
using SkiaSharp;

namespace InternalsViewer.UI.App.Tests.Controls;

[Trait("Category", "Unit")]
[Trait("Area", "Controls")]
public class CanvasViewportTests
{
    [Fact]
    public void Zooming_To_A_Zoom_Keeps_The_Point_Under_The_Pointer()
    {
        var viewport = new CanvasViewport();

        viewport.SetExtent(2000, 2000, 400, 300);

        viewport.SetOffset(100, 50);

        var before = viewport.ToContent(200, 150);

        viewport.ZoomTo(2, 200, 150);

        var after = viewport.ToContent(200, 150);

        Assert.Equal(2f, viewport.Zoom);
        Assert.Equal(before.X, after.X, 3);
        Assert.Equal(before.Y, after.Y, 3);
    }

    [Fact]
    public void Fitting_A_Rectangle_Zooms_And_Centres_On_It()
    {
        var viewport = new CanvasViewport();

        viewport.SetExtent(2000, 2000, 400, 300);

        viewport.Fit(new SKRect(800, 600, 1000, 900));

        var centre = viewport.ToContent(200, 150);

        Assert.Equal(1f, viewport.Zoom, 3);
        Assert.Equal(900f, centre.X, 3);
        Assert.Equal(750f, centre.Y, 3);
    }
}
