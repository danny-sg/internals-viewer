using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using InternalsViewer.UI.App.vNext.Controls.Renderers;
using InternalsViewer.UI.App.vNext.Models;
using InternalsViewer.UI.App.vNext.Helpers;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Linq;
using AllocationOverViewModel = InternalsViewer.UI.App.vNext.ViewModels.AllocationOverViewModel;

namespace InternalsViewer.UI.App.vNext.Controls;
public sealed partial class AllocationControl : UserControl
{
    private static readonly Size ExtentSize = new(80, 10);

    public ExtentLayout Layout { get; set; } = new();

    public static readonly DependencyProperty SizeProperty
        = DependencyProperty.Register(nameof(Size),
                                     typeof(int),
                                     typeof(AllocationControl),
                                     new PropertyMetadata(default, OnPropertyChanged));

    public static readonly DependencyProperty LayersProperty
        = DependencyProperty.Register(nameof(Layers),
                                      typeof(ObservableCollection<AllocationLayer>),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(default, OnPropertyChanged));

    public static readonly DependencyProperty SelectedLayerProperty
        = DependencyProperty.Register(nameof(SelectedLayer),
                                      typeof(AllocationLayer),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(null, OnPropertyChanged));

    public static readonly DependencyProperty AllocationOverProperty
        = DependencyProperty.Register(nameof(AllocationOver),
            typeof(AllocationOverViewModel),
            typeof(AllocationControl),
            new PropertyMetadata(default));

    public int Size
    {
        get => (int)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public AllocationLayer? SelectedLayer
    {
        get => (AllocationLayer?)GetValue(SelectedLayerProperty);
        set => SetValue(SelectedLayerProperty, value);
    }

    public ObservableCollection<AllocationLayer> Layers
    {
        get => (ObservableCollection<AllocationLayer>)GetValue(LayersProperty);
        set => SetValue(LayersProperty, value);
    }

    public AllocationOverViewModel AllocationOver
    {
        get => (AllocationOverViewModel)GetValue(AllocationOverProperty);
        set => SetValue(AllocationOverProperty, value);
    }

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AllocationControl control)
        {
            control.Refresh();
        }
    }

    public int ScrollPosition { get; set; }

    public SKColor BorderColour { get; set; }

    public void Refresh()
    {
        Layout = GetExtentLayout(Size, ExtentSize, (int)AllocationCanvas.ActualWidth, (int)AllocationCanvas.ActualHeight);

        SetScrollBarValues();

        AllocationCanvas.Invalidate();
    }

    public AllocationControl()
    {
        InitializeComponent();

        AllocationCanvas.SizeChanged += AllocationCanvas_SizeChanged;
        PointerWheelChanged += AllocationControl_PointerWheelChanged;

        SetScrollBarValues();

        var borderBrush = Application.Current.Resources["ControlElevationBorderBrush"] as LinearGradientBrush;

        BorderColour = borderBrush?.GradientStops[0].Color.ToSKColor() ?? SystemColors.ActiveBorder.ToSkColor();
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
        if (Layout.HorizontalCount == 0)
        {
            return;
        }

        ScrollBar.IsEnabled = Size > Layout.VisibleCount;
        ScrollBar.SmallChange = Layout.HorizontalCount;
        ScrollBar.LargeChange = (Layout.VerticalCount - 1) * Layout.HorizontalCount;
        ScrollBar.Maximum = Size + Size % Layout.HorizontalCount;
    }

    private void AllocationCanvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
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

        if (SelectedLayer is not null)
        {
            DrawScrollbarMarkers(canvas, Layout, SelectedLayer, e.Info.Width, e.Info.Height);
        }

        using var borderPaint = new SKPaint();

        borderPaint.Color = BorderColour;
        borderPaint.StrokeWidth = 1;
        borderPaint.Style = SKPaintStyle.Stroke;

        canvas.DrawLine(width, 0, width, e.Info.Height, borderPaint);
        //canvas.DrawRect(0, 0, e.Info.Width, e.Info.Height - 1, borderPaint);
    }

    private void DrawScrollbarMarkers(SKCanvas canvas, ExtentLayout layout, AllocationLayer layer, int width, int height)
    {
        var offset = 18;

        // The number of 5 pixel block in the allocation map
        var renderLines = (height - offset * 2) / 5;

        var extentLines = Size / layout.HorizontalCount;

        var extentLinePerRenderLine = extentLines / renderLines;

        var extentPerRenderLine = extentLinePerRenderLine * layout.HorizontalCount;


        using var paint = new SKPaint();
        paint.Color = layer.Colour.ToSkColor();

        for (var i = 0; i < extentLines; i++)
        {
            var extentsFrom = i * extentPerRenderLine;
            var extentsTo = (i + 1) * extentPerRenderLine;

            if (layer.Allocations.Any(a => a > extentsFrom && a <= i + extentsTo))
            {
                var top = offset + i * 5;
                var bottom = offset + (i + 1) * 5;
                var position = new SKRect(width - 10, top, width, bottom);

                canvas.DrawRect(position, paint);
            }
        }
    }

    private void DrawExtents(SKCanvas canvas, ExtentRenderer renderer, ExtentLayout layout)
    {
        var hasSelected = SelectedLayer is not null;

        foreach (var layer in Layers)
        {
            var isSelected = layer == SelectedLayer;

            var alpha = !hasSelected || isSelected ? 255 : 50;

            if (layer is { IsVisible: true })
            {
                var colour = layer.Colour.SetTransparency(alpha);
                var backgroundColour = ColourHelpers.ToBackgroundColour(colour);

                renderer.SetExtentColour(colour, backgroundColour);

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

    /// <summary>
    /// Get the extent at a particular x and y position
    /// </summary>
    private int GetExtentAtPosition(int x, int y)
    {
        return 1 + y / ExtentSize.Height * Layout.HorizontalCount + x / ExtentSize.Width;
    }

    /// <summary>
    /// Get the extent at a particular x and y position
    /// </summary>
    private int GetPageAtPosition(int x, int y)
    {
        return y / ExtentSize.Height * Layout.HorizontalCount * 8 + x / (ExtentSize.Width / 8);
    }

    private void AllocationCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(this).Position;

        var pageId = GetPageAtPosition((int)position.X, (int)position.Y);
        var extentId = GetExtentAtPosition((int)position.X, (int)position.Y);

        var layer = Layers.FirstOrDefault(l => l.Allocations.Contains(extentId));

        AllocationOver = new AllocationOverViewModel
        {
            ExtentId = extentId,
            PageId = pageId,
            LayerName = $"Page 1:{pageId}, Extent: {extentId} - {layer?.Name ?? "Unallocated"}",
        };
    }

    private void ScrollBar_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        var scrollExtent = (int)ScrollBar.Value;

        ScrollPosition = scrollExtent - scrollExtent % (Layout.HorizontalCount - 1);

        AllocationCanvas.Invalidate();
    }
}

public class ExtentLayout
{
    public int HorizontalCount { get; set; }

    public int VerticalCount { get; set; }

    public int RemainingCount { get; set; }

    public int VisibleCount { get; set; }

    public bool IsInitialized { get; set; }
}