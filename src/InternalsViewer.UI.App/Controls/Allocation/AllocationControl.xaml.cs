using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Allocation.Enums;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using Windows.System;
using Windows.UI.Core;
using AllocationOverViewModel = InternalsViewer.UI.App.ViewModels.Allocation.AllocationOverViewModel;
using Color = Windows.UI.Color;

namespace InternalsViewer.UI.App.Controls.Allocation;

public sealed partial class AllocationControl : IDisposable
{
    private const double MinimumZoom = 0.2;
    private const double MaximumZoom = 4;

    private const double MinimumZoomForLines = 0.4;

    // Opacity percentage applied to non-selected layers while a grid selection is active.
    private const int SelectionDimPercent = 40;

    private Size ExtentSize => new((int)(80 * Zoom), (int)(10 * Zoom));

    private ExtentLayout Layout { get; set; } = new();

    public event EventHandler<PageAddressEventArgs>? PageClicked;

    public Color BorderColor
    {
        get => (Color)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    public static readonly DependencyProperty BorderColorProperty
        = DependencyProperty.Register(nameof(BorderColor),
            typeof(Color),
            typeof(AllocationControl),
            new PropertyMetadata(default, OnPropertyChanged));

    public Color GridColor
    {
        get => (Color)GetValue(GridColorProperty);
        set => SetValue(GridColorProperty, value);
    }

    public static readonly DependencyProperty GridColorProperty
        = DependencyProperty.Register(nameof(GridColor),
            typeof(Color),
            typeof(AllocationControl),
            new PropertyMetadata(default, OnPropertyChanged));

    public short FileId
    {
        get => (short)GetValue(FileIdProperty);
        set => SetValue(FileIdProperty, value);
    }

    public static readonly DependencyProperty FileIdProperty
        = DependencyProperty.Register(nameof(FileId),
            typeof(short),
            typeof(AllocationControl),
            null);

    public bool IsTooltipEnabled
    {
        get => (bool)GetValue(IsTooltipEnabledProperty);
        set => SetValue(IsTooltipEnabledProperty, value);
    }

    public static readonly DependencyProperty IsTooltipEnabledProperty
        = DependencyProperty.Register(nameof(IsTooltipEnabled),
            typeof(bool),
            typeof(AllocationControl),
            null);

    /// <summary>
    /// Gets or sets the number of extents in the allocation map.
    /// </summary>
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

    public ObservableCollection<AllocationLayer> SelectedLayers
    {
        get => (ObservableCollection<AllocationLayer>)GetValue(SelectedLayersProperty);
        set => SetValue(SelectedLayersProperty, value);
    }

    public static readonly DependencyProperty SelectedLayersProperty
        = DependencyProperty.Register(nameof(SelectedLayers),
                                      typeof(ObservableCollection<AllocationLayer>),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(null, OnPropertyChanged));

    public PfsChain PfsChain
    {
        get => (PfsChain)GetValue(PfsChainProperty);
        set => SetValue(PfsChainProperty, value);
    }

    public static readonly DependencyProperty PfsChainProperty
        = DependencyProperty.Register(nameof(PfsChain),
                                      typeof(PfsChain),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(default, OnPropertyChanged));

    public bool IsPfsVisible
    {
        get => (bool)GetValue(IsPfsVisibleProperty);
        set => SetValue(IsPfsVisibleProperty, value);
    }

    public static readonly DependencyProperty IsPfsVisibleProperty
        = DependencyProperty.Register(nameof(IsPfsVisible),
                                      typeof(bool),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(default, OnPropertyChanged));

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public long SequenceFrom
    {
        get => (long)GetValue(SequenceFromProperty);
        set => SetValue(SequenceFromProperty, value);
    }

    public long SequenceTo
    {
        get => (long)GetValue(SequenceToProperty);
        set => SetValue(SequenceToProperty, value);
    }

    private static readonly DependencyProperty ZoomProperty
        = DependencyProperty.Register(nameof(Zoom),
                                      typeof(double),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(1D, OnPropertyChanged));

    private static readonly DependencyProperty SequenceFromProperty
        = DependencyProperty.Register(nameof(SequenceFrom),
            typeof(long),
            typeof(AllocationControl),
            new PropertyMetadata(null, OnPropertyChanged));

    private static readonly DependencyProperty SequenceToProperty
        = DependencyProperty.Register(nameof(SequenceTo),
                                      typeof(long),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(null, OnPropertyChanged));

    public AllocationOverViewModel AllocationOver { get; } = new();

    private int PageCount => ExtentCount * 8;

    // Add these fields near the top of the class, alongside ScrollPosition
    private AllocationRenderer? _renderer;
    private SKPaint? _borderPaint;
    private Size _lastExtentSize;
    private Color _lastGridColor;

    private AllocationRenderer GetOrCreateRenderer()
    {
        var extentSize = ExtentSize;
        var gridColor = GridColor;

        if (_renderer is null || extentSize != _lastExtentSize || gridColor != _lastGridColor)
        {
            _renderer?.Dispose();
            _borderPaint?.Dispose();

            _renderer = new AllocationRenderer(gridColor.ToColor(), extentSize);
            _renderer.IsDrawBorder = true;

            _borderPaint = new SKPaint
            {
                Color = BorderColor.ToSkColor(),
                StrokeWidth = 1,
                Style = SKPaintStyle.Stroke
            };

            _lastExtentSize = extentSize;
            _lastGridColor = gridColor;
        }

        return _renderer;
    }

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AllocationControl control)
        {
            if (e.Property == LayersProperty)
            {
                if (e.OldValue is ObservableCollection<AllocationLayer> old)
                {
                    old.CollectionChanged -= control.OnLayersChanged;
                }

                if (e.NewValue is ObservableCollection<AllocationLayer> next)
                {
                    next.CollectionChanged += control.OnLayersChanged;
                }
            }

            if (e.Property == SelectedLayersProperty)
            {
                if (e.OldValue is ObservableCollection<AllocationLayer> old)
                {
                    old.CollectionChanged -= control.OnSelectedLayersChanged;
                }

                if (e.NewValue is ObservableCollection<AllocationLayer> next)
                {
                    next.CollectionChanged += control.OnSelectedLayersChanged;
                }
            }

            control.Refresh();
        }
    }

    private void OnLayersChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Refresh();

    private void OnSelectedLayersChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Refresh();

    private int ScrollPosition { get; set; }

    public AllocationControl()
    {
        InitializeComponent();

        AllocationCanvas.PaintSurface += AllocationCanvas_PaintSurface;
        AllocationCanvas.PointerMoved += AllocationCanvas_PointerMoved;
        AllocationCanvas.PointerPressed += AllocationCanvas_PointerPressed;
        AllocationCanvas.PointerExited += AllocationCanvas_PointerExited;
        AllocationCanvas.PointerEntered += AllocationCanvas_PointerEntered;
        AllocationCanvas.SizeChanged += AllocationCanvas_SizeChanged;

        PointerWheelChanged += AllocationControl_PointerWheelChanged;

        // The Skia canvas does not repaint itself after being reparented (e.g. when its tab is dragged
        // into a split). Refresh on (re)load so the map is redrawn.
        Loaded += (_, _) => Refresh();

        SetScrollBarValues();
    }

    private void Refresh()
    {
        Layout = GetExtentLayout(ExtentCount,
                                 ExtentSize,
                                 (int)AllocationCanvas.ActualWidth,
                                 (int)AllocationCanvas.ActualHeight);

        SetScrollBarValues();

        AllocationCanvas.Invalidate();
    }

    private void AllocationControl_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);

        var isControlPressed = state.HasFlag(CoreVirtualKeyStates.Down);

        if (isControlPressed)
        {
            var newZoom = Zoom + e.GetCurrentPoint(this).Properties.MouseWheelDelta / 1000D;

            if (newZoom is >= MinimumZoom and <= MaximumZoom)
            {
                Zoom = newZoom;
            }
        }
        else if (ScrollBar.IsEnabled)
        {
            ScrollBar.Value -= e.GetCurrentPoint(this).Properties.MouseWheelDelta;
        }
    }

    private void AllocationCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Layout = GetExtentLayout(ExtentCount, ExtentSize, (int)e.NewSize.Width, (int)e.NewSize.Height);

        SetScrollBarValues();

        AllocationCanvas.Invalidate();
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

    private void AllocationCanvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        canvas.Clear(SKColors.Transparent);

        var renderLayout = GetExtentLayout(ExtentCount - ScrollPosition,
                                           ExtentSize,
                                           (int)AllocationCanvas.ActualWidth,
                                           (int)AllocationCanvas.ActualHeight);

        var renderer = GetOrCreateRenderer();

        renderer.DrawBackgroundExtents(canvas,
                                       renderLayout.HorizontalCount,
                                       renderLayout.VerticalCount,
                                       renderLayout.RemainingCount);

        var width = renderLayout.HorizontalCount * ExtentSize.Width;

        DrawExtents(canvas, renderer, renderLayout);

        if (IsPfsVisible)
        {
            using var pfsRenderer = new PfsRenderer(ExtentSize with { Width = ExtentSize.Width / 8 });

            DrawPfs(canvas, pfsRenderer, renderLayout);
        }

        if (Zoom >= MinimumZoomForLines)
        {
            renderer.DrawPageLines(canvas,
                                   renderLayout.HorizontalCount,
                                   renderLayout.VerticalCount,
                                   renderLayout.RemainingCount);
        }

        if (SelectedLayers is { Count: > 0 })
        {
            foreach (var selectedLayer in SelectedLayers)
            {
                DrawScrollbarMarkers(canvas, Layout, selectedLayer, e.Info.Width, e.Info.Height);
            }
        }

        canvas.DrawLine(width, 0, width, e.Info.Height, _borderPaint);
    }

    private void DrawScrollbarMarkers(SKCanvas canvas,
                                      ExtentLayout layout,
                                      AllocationLayer layer,
                                      int width,
                                      int height)
    {
        // Offset accounting for the scrollbar buttons
        var offset = 18;

        // Size of each block next to the scrollbar
        var blockSize = 4;

        // The number of [Block Size] pixel block in the allocation map
        var renderLines = (height - (offset)) / blockSize;

        var extentLines = ExtentCount / layout.VerticalCount;

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

            foreach (var allocationChain in layer.AllocationChains)
            {
                if (allocationChain.AnyExtentsAllocated(extentsFrom, extentsTo, FileId, layer.IsInverted)
                    || layer.SinglePages.Any(a => a.PageId > pagesFrom && a.PageId <= pagesTo))
                {
                    //}

                    //if (layer.Allocations.Any(a => a.FileId == FileId && a.ExtentId > extentsFrom && a.ExtentId <= i + extentsTo)
                    //                          || layer.SinglePages.Any(a => a.PageId > pagesFrom && a.PageId <= pagesTo))
                    //{
                    var top = offset + i * blockSize;
                    var bottom = offset + (i + 1) * blockSize;

                    var position = new SKRect(width - blockSize * 2, top, width, bottom);

                    canvas.DrawRect(position, paint);
                }
            }
        }
    }

    private void DrawExtents(SKCanvas canvas, AllocationRenderer renderer, ExtentLayout layout)
    {
        var hasSelected = SelectedLayers is { Count: > 0 };
        var selectedSet = hasSelected ? new HashSet<AllocationLayer>(SelectedLayers!) : null;

        foreach (var layer in Layers)
        {
            if (!layer.IsVisible || layer.Opacity == 0)
            {
                continue;
            }

            var isSelected = selectedSet?.Contains(layer) ?? false;

            // Opacity is a 0-100 percentage. With no grid selection every layer honours its own
            // opacity - this is what lets overlay mode dim non-overlay layers while keeping them
            // visible. When layers are selected the selected ones show fully and the rest dim.
            var opacityPercent = !hasSelected
                ? layer.Opacity
                : isSelected ? 100 : Math.Min((int)layer.Opacity, SelectionDimPercent);

            var alpha = (byte)(opacityPercent * 255 / 100);
            var colour = layer.Colour.SetTransparency(alpha);

            renderer.SetAllocationColour(colour, ColourHelpers.ToBackgroundColour(colour));

            var chains = layer.AllocationChains;

            if (chains.Count > 0)
            {
                var isInverted = layer.IsInverted;
                var fileId = FileId;

                switch (chains)
                {
                    case [IamChain single]:
                        DrawExtentsCore(canvas, renderer, layout, single, isInverted, fileId);
                        break;
                    case [AllocationChain single]:
                        DrawExtentsCore(canvas, renderer, layout, single, isInverted, fileId);
                        break;
                    case [BitmapAllocation single]:
                        DrawExtentsCore(canvas, renderer, layout, single, isInverted, fileId);
                        break;
                    default:
                        DrawExtentsMulti(canvas, renderer, layout, chains, isInverted, fileId);
                        break;
                }
            }

            foreach (var page in layer.SinglePages)
            {
                if (page.FileId == FileId)
                {
                    renderer.DrawPage(canvas, GetPagePosition(page.PageId - (ScrollPosition * 8), layout));
                }
            }

            foreach (var page in layer.PageSpans
                         .Where(l => ((l.SequenceFrom >= SequenceFrom && l.SequenceTo <= SequenceTo)
                                         || SequenceFrom == 0 && SequenceTo == 0)))
            {
                if (page.Address.FileId == FileId)
                {
                    renderer.DrawPage(canvas, GetPagePosition(page.Address.PageId - (ScrollPosition * 8), layout));
                }
            }
        }
    }

    private void DrawExtentsCore<TChain>(SKCanvas canvas,
                                         AllocationRenderer renderer,
                                         ExtentLayout layout,
                                         TChain chain,
                                         bool isInverted,
                                         short fileId)
        where TChain : class, IAllocationChain
    {
        for (var i = ScrollPosition; i < ScrollPosition + layout.VisibleCount; i++)
        {
            if (chain.IsExtentAllocated(i, fileId, isInverted))
            {
                renderer.DrawExtent(canvas, GetExtentPosition(i - ScrollPosition, layout));
            }
        }
    }

    private void DrawExtentsMulti(SKCanvas canvas,
                                  AllocationRenderer renderer,
                                  ExtentLayout layout,
                                  List<IAllocationChain> chains,
                                  bool isInverted,
                                  short fileId)
    {
        var chainCount = chains.Count;

        for (var i = ScrollPosition; i < ScrollPosition + layout.VisibleCount; i++)
        {
            var toRender = true;

            for (var c = 0; c < chainCount; c++)
            {
                if (!chains[c].IsExtentAllocated(i, fileId, isInverted))
                {
                    toRender = false;
                    break;
                }
            }

            if (toRender)
            {
                renderer.DrawExtent(canvas, GetExtentPosition(i - ScrollPosition, layout));
            }
        }
    }

    private void DrawPfs(SKCanvas canvas, PfsRenderer renderer, ExtentLayout layout)
    {
        for (var i = 0; i < layout.VisibleCount * 8; i++)
        {
            var pageId = i + (ScrollPosition * 8);

            var pfs = PfsChain.GetPageStatus(pageId);

            var position = GetPagePosition(i, layout);

            renderer.DrawPfs(canvas, position, pfs);
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

        var row = (extentId) / horizontalCount;
        var column = (extentId) % horizontalCount;

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

    private static ExtentLayout GetExtentLayout(int extentCount, Size extentSize, decimal width, decimal height)
    {
        var extentsHorizontal = (int)Math.Floor(width / extentSize.Width);
        var rowsVisible = (int)Math.Ceiling(height / extentSize.Height);

        if (extentsHorizontal == 0 || rowsVisible == 0 || extentCount == 0)
        {
            return new ExtentLayout();
        }

        if (extentsHorizontal > extentCount)
        {
            extentsHorizontal = extentCount;
        }

        var visibleCount = Math.Min(extentsHorizontal * rowsVisible, extentCount);

        var fullRows = extentCount / extentsHorizontal;
        var lastRowExtents = extentCount % extentsHorizontal;

        var extentsVertical = Math.Min(fullRows, rowsVisible);
        var extentsRemaining = fullRows < rowsVisible ? lastRowExtents : 0;

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
        return y / ExtentSize.Height * Layout.HorizontalCount + x / ExtentSize.Width + ScrollPosition;
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

        var layer = Layers.FirstOrDefault(
            l => l.AllocationChains.Any(a => a.IsExtentAllocated(extentId, FileId, l.IsInverted))
                 || l.SinglePages.Any(p => p.PageId == pageId && p.FileId == FileId));

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
                layerName = $"{layer?.Name ?? string.Empty}";
                break;
        }

        AllocationOver.ExtentId = extentId;
        AllocationOver.PageId = pageId;
        AllocationOver.LayerName = layerName;
        AllocationOver.PfsValue = PfsChain?.GetPageStatus(pageId) ?? PfsByte.Unknown;

        if (IsTooltipEnabled)
        {
            TooltipPopup.HorizontalOffset = position.X + 5;
            TooltipPopup.VerticalOffset = position.Y + 5;
        }
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
            PageClicked?.Invoke(this, new PageAddressEventArgs(FileId, pageId, null));
        }
    }

    private void AllocationCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        AllocationOver.IsOpen = false;
    }

    private void AllocationCanvas_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        AllocationOver.IsOpen = IsTooltipEnabled;
    }

    public void Dispose()
    {
        _renderer?.Dispose();
        _borderPaint?.Dispose();

        AllocationCanvas.SizeChanged -= AllocationCanvas_SizeChanged;
        PointerWheelChanged -= AllocationControl_PointerWheelChanged;
        AllocationCanvas.PaintSurface -= AllocationCanvas_PaintSurface;
        AllocationCanvas.PointerMoved -= AllocationCanvas_PointerMoved;
        AllocationCanvas.PointerPressed -= AllocationCanvas_PointerPressed;
        AllocationCanvas.PointerExited -= AllocationCanvas_PointerExited;
        AllocationCanvas.PointerEntered -= AllocationCanvas_PointerEntered;
        AllocationCanvas.SizeChanged -= AllocationCanvas_SizeChanged;
    }
}

public sealed class PageAddressEventArgs(short fileId, int pageId, ushort? slot) : EventArgs
{
    public PageAddressEventArgs(short fileId, int pageId)
        : this(fileId, pageId, null)
    {
    }

    public PageAddressEventArgs(PageAddress pageAddress)
        : this(pageAddress.FileId, pageAddress.PageId, null)
    {
    }

    public short FileId { get; } = fileId;

    public int PageId { get; } = pageId;

    public ushort? Slot { get; init; } = slot;

    public string Tag { get; set; } = string.Empty;

    public PageAddress PageAddress => new(FileId, PageId);
}

public sealed class ExtentLayout
{
    public int HorizontalCount { get; init; }

    public int VerticalCount { get; init; }

    public int RemainingCount { get; init; }

    /// <summary>
    /// Number of extents visible
    /// </summary>
    public int VisibleCount { get; init; }

    public bool IsInitialized { get; set; }
}