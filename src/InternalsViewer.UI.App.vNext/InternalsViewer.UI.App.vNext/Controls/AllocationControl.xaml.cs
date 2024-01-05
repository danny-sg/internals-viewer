using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;
using System.Drawing;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace InternalsViewer.UI.App.vNext.Controls;
public sealed partial class AllocationControl : UserControl
{
    private static readonly Size ExtentSize = new(80, 10);

    public ExtentLayout ExtentLayout { get; set; }
    public AllocationControl()
    {
        InitializeComponent();
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var surface = e.Surface;
        var canvas = surface.Canvas;

        canvas.Clear();
        canvas.Clear(SKColors.Transparent);

        using var extentRenderer = new ExtentRenderer(Color.Red, Color.Blue, Color.White, ExtentSize);

        extentRenderer.IsDrawBorder = true;

        var layout = GetExtentLayout(500, ExtentSize, e.Info.Width, e.Info.Height);

        extentRenderer.DrawBackgroundExtents(canvas, layout.HorizontalCount, layout.VerticalCount, layout.RemainingCount);

        var width = (layout.HorizontalCount - 1) * ExtentSize.Width;

        canvas.DrawLine(width, 0, width, e.Info.Height, new SKPaint { Color = new SKColor(220, 220, 220), StrokeWidth = 1 });
    }



    public void Initialize()
    {
        var extentCount = 5000;
        ExtentLayout = GetExtentLayout(extentCount, ExtentSize, (int)Width, (int)Height);

        ScrollBar.SmallChange = ExtentLayout.HorizontalCount;
        ScrollBar.LargeChange = (ExtentLayout.VerticalCount - 1) * ExtentLayout.HorizontalCount;

        ScrollBar.Maximum = extentCount + ExtentLayout.HorizontalCount;
    }

    public ExtentLayout GetExtentLayout(int extentCount, Size extentSize, decimal width, decimal height)
    {
        var extentsHorizontal = (int)Math.Ceiling(width / extentSize.Width);
        var extentsVertical = (int)Math.Ceiling(height / extentSize.Height);

        if (extentsHorizontal == 0 | extentsVertical == 0 | extentCount == 0)
        {
            return new ExtentLayout();
        }

        if (extentsHorizontal == 0)
        {
            extentsHorizontal = 1;
        }

        if (extentsHorizontal > extentCount)
        {
            extentsHorizontal = extentCount;
        }

        if (extentsVertical > extentCount / extentsHorizontal)
        {
            extentsVertical = extentCount / extentsHorizontal;
        }

        var extentsRemaining = extentCount - (extentsHorizontal * extentsVertical);

        return new ExtentLayout
        {
            HorizontalCount = extentsHorizontal,
            VerticalCount = extentsVertical,
            RemainingCount = extentsRemaining
        };
    }

    private void AllocationCanvas_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(e.GetCurrentPoint(this).Position);
    }
}

public class ExtentLayout
{
    public int HorizontalCount { get; set; }

    public int VerticalCount { get; set; }

    public int RemainingCount { get; set; }
}