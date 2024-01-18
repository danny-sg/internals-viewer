using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using InternalsViewer.UI.App.vNext.Controls.Page;
using InternalsViewer.UI.App.vNext.Controls.Renderers;
using InternalsViewer.UI.App.vNext.Helpers;
using InternalsViewer.UI.App.vNext.Models;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using AllocationOverViewModel = InternalsViewer.UI.App.vNext.ViewModels.Allocation.AllocationOverViewModel;

namespace InternalsViewer.UI.App.vNext.Controls.Allocation;

public sealed partial class AllocationControl
{
    private static readonly Size ExtentSize = new(80, 10);

    public ExtentLayout Layout { get; set; } = new();

    public event EventHandler<PageClickedEventArgs>? PageClicked;

    public int ExtentCount
    {
        get => (int)GetValue(ExtentCountProperty);
        set => SetValue(ExtentCountProperty, value);
    }

    public static readonly DependencyProperty ExtentCountProperty
        = DependencyProperty.Register(nameof(ExtentCount),
                                     typeof(int),
                                     typeof(AllocationControl),
                                     new PropertyMetadata(default, OnPropertyChanged));

    public ObservableCollection<AllocationLayer> Layers
    {
        get => (ObservableCollection<AllocationLayer>)GetValue(LayersProperty);
        set => SetValue(LayersProperty, value);
    }

    public static readonly DependencyProperty LayersProperty
        = DependencyProperty.Register(nameof(Layers),
                                      typeof(ObservableCollection<AllocationLayer>),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(default, OnPropertyChanged));
    
    public AllocationLayer? SelectedLayer
    {
        get => (AllocationLayer?)GetValue(SelectedLayerProperty);
        set => SetValue(SelectedLayerProperty, value);
    }

    public static readonly DependencyProperty SelectedLayerProperty
        = DependencyProperty.Register(nameof(SelectedLayer),
                                      typeof(AllocationLayer),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(null, OnPropertyChanged));

    public AllocationOverViewModel AllocationOver
    {
        get => (AllocationOverViewModel)GetValue(AllocationOverProperty);
        set => SetValue(AllocationOverProperty, value);
    }

    public static readonly DependencyProperty AllocationOverProperty
        = DependencyProperty.Register(nameof(AllocationOver),
            typeof(AllocationOverViewModel),
            typeof(AllocationControl),
            new PropertyMetadata(default));

    public int PageCount => ExtentCount * 8;

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
        Layout = GetExtentLayout(ExtentCount, ExtentSize, (int)AllocationCanvas.ActualWidth, (int)AllocationCanvas.ActualHeight);

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
        Layout = GetExtentLayout(ExtentCount, ExtentSize, (int)e.NewSize.Width, (int)e.NewSize.Height);

        SetScrollBarValues();
    }

    private void SetScrollBarValues()
    {
        if (Layout.HorizontalCount == 0)
        {
            return;
        }

        ScrollBar.IsEnabled = ExtentCount > Layout.VisibleCount;
        ScrollBar.SmallChange = Layout.HorizontalCount;
        ScrollBar.LargeChange = (Layout.VerticalCount - 1) * Layout.HorizontalCount;
        ScrollBar.Maximum = ExtentCount + ExtentCount % Layout.HorizontalCount;
    }

    private void AllocationCanvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var surface = e.Surface;
        var canvas = surface.Canvas;

        canvas.Clear();
        canvas.Clear(SKColors.Transparent);

        using var extentRenderer = new AllocationRenderer(Color.Red, Color.Blue, Color.White, ExtentSize);

        extentRenderer.IsDrawBorder = true;

        extentRenderer.DrawBackgroundExtents(canvas, Layout.HorizontalCount, Layout.VerticalCount, Layout.RemainingCount);

        var width = Layout.HorizontalCount * ExtentSize.Width;

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
        // Offset accounting for the scrollbar buttons
        var offset = 18;

        // Size of each block next to the scrollbar
        var blockSize = 4;

        // The number of [Block Size] pixel block in the allocation map
        var renderLines = (height - (offset)) / blockSize;

        var extentLines = ExtentCount / layout.HorizontalCount;

        var extentLinePerRenderLine = extentLines / renderLines;

        var extentPerRenderLine = extentLinePerRenderLine * layout.HorizontalCount;

        using var paint = new SKPaint();

        paint.Color = layer.Colour.ToSkColor();

        for (var i = 0; i < renderLines; i++)
        {
            var extentsFrom = i * extentPerRenderLine;
            var extentsTo = (i + 1) * extentPerRenderLine;
            var pagesFrom = extentsFrom * 8;
            var pagesTo = extentsTo * 8;

            if (layer.Allocations.Any(a => a > extentsFrom && a <= i + extentsTo)
                                      || layer.SinglePages.Any(a => a.PageId > pagesFrom && a.PageId <= pagesTo))
            {
                var top = offset + i * blockSize;
                var bottom = offset + (i + 1) * blockSize;
                var position = new SKRect(width - blockSize * 2, top, width, bottom);

                canvas.DrawRect(position, paint);
            }
        }
    }

    private void DrawExtents(SKCanvas canvas, AllocationRenderer renderer, ExtentLayout layout)
    {
        var hasSelected = SelectedLayer is not null;

        foreach (var layer in Layers)
        {
            var isSelected = layer == SelectedLayer;

            var alpha = !hasSelected || isSelected ? 255 : 25;

            if (layer is { IsVisible: true })
            {
                var colour = layer.Colour.SetTransparency(alpha);
                var backgroundColour = ColourHelpers.ToBackgroundColour(colour);

                renderer.SetAllocationColour(colour, backgroundColour);

                foreach (var extent in layer.Allocations)
                {
                    renderer.DrawExtent(canvas, GetExtentPosition(extent - ScrollPosition, layout));
                }

                foreach (var page in layer.SinglePages)
                {
                    renderer.DrawPage(canvas, GetPagePosition(page.PageId - ScrollPosition * 8, layout));

                }
            }
        }
    }

    private SKRect GetPagePosition(int pageId, ExtentLayout layout)
    {
        // Number of pages horizontally
        var horizontalCount = layout.HorizontalCount * 8;

        var row = (pageId) / horizontalCount;
        var column = (pageId) % horizontalCount;

        var pageWidth = ExtentSize.Width / 8F;

        var left = column * pageWidth;
        var top = row * ExtentSize.Height;

        var right = left + pageWidth;
        var bottom = top + ExtentSize.Height;

        if (horizontalCount > 1)
        {
            return new SKRect(left, top, right, bottom);
        }

        return new SKRect(0, 0, pageWidth, ExtentSize.Height);
    }

    /// <summary>
    /// Get Rectangle for a particular extent
    /// </summary>
    private SKRect GetExtentPosition(int extentId, ExtentLayout layout)
    {
        var horizontalCount = layout.HorizontalCount;

        var row = (extentId - 1) / horizontalCount;
        var column = (extentId - 1) % horizontalCount;

        var left = column * ExtentSize.Width;
        var top = row * ExtentSize.Height;

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
        var extentsHorizontal = (int)Math.Floor(width / extentSize.Width);
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
        return 1 + y / ExtentSize.Height * Layout.HorizontalCount + x / ExtentSize.Width + ScrollPosition;
    }

    /// <summary>
    /// Get the extent at a particular x and y position
    /// </summary>
    private int GetPageAtPosition(int x, int y)
    {
        return y / ExtentSize.Height * Layout.HorizontalCount * 8 + x / (ExtentSize.Width / 8) + ScrollPosition * 8;
    }

    private void AllocationCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(this).Position;

        var pageId = GetPageAtPosition((int)position.X, (int)position.Y);
        var extentId = GetExtentAtPosition((int)position.X, (int)position.Y);

        var layer = Layers.FirstOrDefault(l => l.Allocations.Contains(extentId));

        string layerName;

        switch (pageId)
        {
            case 0:
                layerName = "File Header";
                break;
            case 1:
                layerName = "PFS";
                break;
            case 2:
                layerName = "GAM";
                break;
            case 3:
                layerName = "SGAM";
                break;
            case 4:
                layerName = "DCM";
                break;
            case 5:
                layerName = "BCM";
                break;
            case 6:
                layerName = "Differential Change Map";
                break;
            case 7:
                layerName = "Bulk Change Map";
                break;
            default:
                layerName = $"Page 1:{pageId}, Extent: {extentId} - {layer?.Name ?? "Unallocated"}";
                break;
        }

        AllocationOver = new AllocationOverViewModel
        {
            ExtentId = extentId,
            PageId = pageId,
            LayerName = layerName
        };
    }

    private void ScrollBar_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        var scrollExtent = (int)ScrollBar.Value;

        ScrollPosition = scrollExtent - scrollExtent % Layout.HorizontalCount;

        AllocationCanvas.Invalidate();
    }

    private void AllocationCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(this).Position;

        var pageId = GetPageAtPosition((int)position.X, (int)position.Y);

        if (pageId <= PageCount)
        {
            PageClicked?.Invoke(this, new PageClickedEventArgs(pageId));
        }
    }

    private void AllocationCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        AllocationOver = new AllocationOverViewModel();
    }
}

public class PageClickedEventArgs(int pageId) : EventArgs
{
    public int PageId { get; set; } = pageId;
}

public class ExtentLayout
{
    public int HorizontalCount { get; set; }

    public int VerticalCount { get; set; }

    public int RemainingCount { get; set; }

    public int VisibleCount { get; set; }

    public bool IsInitialized { get; set; }
}