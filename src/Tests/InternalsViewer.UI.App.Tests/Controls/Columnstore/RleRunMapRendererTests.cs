using InternalsViewer.UI.App.Controls.Columnstore.Segment;
using InternalsViewer.UI.App.Models.Columnstore.Segment;
using SkiaSharp;

namespace InternalsViewer.UI.App.Tests.Controls.Columnstore;

public class RleRunMapRendererTests
{
    private const int Width = 400;

    /// <summary>
    /// A run that reads a sequence walks its store as it goes, so it cannot be one flat colour
    /// </summary>
    [Fact]
    public void Sweeps_A_Bit_Pack_Read_Across_Its_Width()
    {
        // One literal run then one bit packed run, which is the shape of an ordinary dictionary segment
        List<RleRunDetail> runs =
        [
            new(0, 0, 1000, true, 42, 0),
            new(1, 1000, 9000, false, 0, 8),
            new(2, 10000, 0, true, 0, 16)
        ];

        var colours = Render(runs);

        Assert.True(colours.Skip(45).Distinct().Count() > 1,
                    $"bit pack read drew {colours.Skip(45).Distinct().Count()} colour(s)");
    }

    [Fact]
    public void Sweeps_A_Variable_Length_Data_Read_Across_Its_Width()
    {
        List<RleRunDetail> runs =
        [
            new(0, 0, 1000, true, 0, 0, new InternalsViewer.Internals.Columnstore.Segments.SegmentPageSlot(0, 0), 0),
            new(1, 1000, 9000, false, 0, 8, new InternalsViewer.Internals.Columnstore.Segments.SegmentPageSlot(0, 1), 1),
            new(2, 10000, 0, true, 0, 16)
        ];

        var colours = Render(runs);

        Assert.True(colours.Skip(45).Distinct().Count() > 1,
                    $"variable length data read drew {colours.Skip(45).Distinct().Count()} colour(s)");
    }

    private static List<SKColor> Render(List<RleRunDetail> runs)
    {
        using var renderer = new RleRunMapRenderer();

        using var bitmap = new SKBitmap(Width, 60);

        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.Black);

        renderer.Draw(canvas, runs, Width, 0, RleRunMapRenderer.TotalRows(runs));

        // The lower track is where a run that covers a sequence is drawn
        var row = 32;

        return Enumerable.Range(59, 340).Select(x => bitmap.GetPixel(x, row)).ToList();
    }
}
