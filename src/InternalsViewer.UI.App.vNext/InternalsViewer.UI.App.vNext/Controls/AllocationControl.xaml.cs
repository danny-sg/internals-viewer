using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using InternalsViewer.UI.App.vNext.Controls.Renderers;
using InternalsViewer.UI.App.vNext.Models;
using InternalsViewer.UI.App.vNext.Helpers;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Input;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace InternalsViewer.UI.App.vNext.Controls;
public sealed partial class AllocationControl : UserControl
{
    private static readonly Size ExtentSize = new(80, 10);

    public ExtentLayout Layout { get; set; } = new();

    public int Size
    {
        get => size;
        set
        {
            size = value;

            // Invalidate to re-draw the canvas
            AllocationCanvas.Invalidate();
        }
    }

    public ObservableCollection<AllocationLayer> Layers { get; set; } = new();

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size),
                                                                                         typeof(int),
                                                                                         typeof(AllocationControl),
                                                                                         PropertyMetadata.Create(() => 0));

    private int size;

    public int ScrollPosition { get; set; }

    public AllocationControl()
    {
        InitializeComponent();

        AllocationCanvas.SizeChanged += AllocationCanvas_SizeChanged;   
        PointerWheelChanged += AllocationControl_PointerWheelChanged;
    }

    private void AllocationControl_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        ScrollBar.Value -= e.GetCurrentPoint(this).Properties.MouseWheelDelta;
    }

    private void AllocationCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Layout = GetExtentLayout(Size, ExtentSize, (int)e.NewSize.Width, (int)e.NewSize.Height);

        SetScrollBarValues();
    }

    private void SetScrollBarValues()
    {
        ScrollBar.IsEnabled = Size > Layout.VisibleCount;
        ScrollBar.SmallChange = Layout.HorizontalCount;
        ScrollBar.LargeChange = (Layout.VerticalCount - 1) * Layout.HorizontalCount;
        ScrollBar.Maximum = Size + (Size % Layout.HorizontalCount);
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var surface = e.Surface;
        var canvas = surface.Canvas;

        canvas.Clear();
        canvas.Clear(SKColors.Transparent);

        using var extentRenderer = new ExtentRenderer(Color.Red, Color.Blue, Color.White, ExtentSize);

        extentRenderer.IsDrawBorder = true;

        extentRenderer.DrawBackgroundExtents(canvas, Layout.HorizontalCount, Layout.VerticalCount, Layout.RemainingCount);

        var width = (Layout.HorizontalCount - 1) * ExtentSize.Width;

        DrawExtents(canvas, extentRenderer, Layout);

        extentRenderer.DrawPageLines(canvas, Layout.HorizontalCount, Layout.VerticalCount, Layout.RemainingCount);

        using var borderPaint = new SKPaint { Color = new SKColor(220, 220, 220), StrokeWidth = 1 };

        canvas.DrawLine(width, 0, width, e.Info.Height, borderPaint);
        canvas.DrawLine(0, 0, width, 0, borderPaint);
    }

    private void DrawExtents(SKCanvas canvas, ExtentRenderer renderer, ExtentLayout layout)
    {
        foreach (var layer in Layers)
        {
            if (layer is { IsVisible: true })
            {
                renderer.SetExtentColour(layer.Colour, ColourHelpers.ToBackgroundColour(layer.Colour));

                foreach (var extent in layer.Allocations)
                {
                    renderer.DrawExtent(canvas, GetExtentPosition(extent - ScrollPosition, layout));
                }
            }
        }
    }

    /// <summary>
    /// Get Rectangle for a particular extent
    /// </summary>
    private SKRect GetExtentPosition(int extent, ExtentLayout layout)
    {
        var horizontalCount = layout.HorizontalCount;


        var left = extent * ExtentSize.Width % ((horizontalCount - 1) * ExtentSize.Width);
        var top = (int)Math.Floor((decimal)extent / horizontalCount) * ExtentSize.Height;

        var right = left + ExtentSize.Width;
        var bottom = top + ExtentSize.Height;


        if (horizontalCount > 1)
        {
            return new SKRect(left, top, right, bottom);
        }

        return new SKRect(0, 0, ExtentSize.Width, ExtentSize.Height);
    }

    public ExtentLayout GetExtentLayout(int extentCount, Size extentSize, decimal width, decimal height)
    {
        var extentsHorizontal = (int)Math.Ceiling(width / extentSize.Width);
        var extentsVertical = (int)Math.Ceiling(height / extentSize.Height);

        var visibleCount = extentsHorizontal * extentsVertical;

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

        var extentsRemaining = extentCount - visibleCount;

        return new ExtentLayout
        {
            HorizontalCount = extentsHorizontal,
            VerticalCount = extentsVertical,
            RemainingCount = extentsRemaining,
            VisibleCount = visibleCount
        };
    }

    private void AllocationCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(e.GetCurrentPoint(this).Position);
    }

    private void ScrollBar_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        var scrollExtent = (int)ScrollBar.Value;

        ScrollPosition = scrollExtent - scrollExtent % (Layout.HorizontalCount -1);

        AllocationCanvas.Invalidate();
    }
}

public class ExtentLayout
{
    public int HorizontalCount { get; set; }

    public int VerticalCount { get; set; }

    public int RemainingCount { get; set; }

    public int VisibleCount { get; set; }
}