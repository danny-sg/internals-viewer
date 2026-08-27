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
using InternalsViewer.UI.App.ViewModels.Allocation;
using Color = Windows.UI.Color;
using InternalsViewer.UI.App.Models.Allocations;

namespace InternalsViewer.UI.App.Controls.Allocation;

public sealed partial class AllocationControl : IDisposable
{
    private const double MinimumZoom = 0.2;

    private const double MaximumZoom = 4;

    private const double MinimumZoomForLines = 0.4;

    private const int ScrollBufferRows = 2;

    private const int UnitZoomPageWidth = 10;

    /// <summary>
    /// How far either side of the zoom asked for a better fitting one is looked for
    /// </summary>
    private const double FitZoomTolerance = 0.25;

    private const float MinHeatmapChromaRatio = 0.15F;

    public static readonly DependencyProperty BorderColorProperty
        = DependencyProperty.Register(nameof(BorderColor),
            typeof(Color),
            typeof(AllocationControl),
            new PropertyMetadata(null, OnPropertyChanged));

    public Color BorderColor
    {
        get => (Color)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    public static readonly DependencyProperty GridColorProperty 
        = DependencyProperty.Register(nameof(GridColor),
                                      typeof(Color),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(null, OnPropertyChanged));

    public Color GridColor
    {
        get => (Color)GetValue(GridColorProperty);
        set => SetValue(GridColorProperty, value);
    }

    public static readonly DependencyProperty FileIdProperty
        = DependencyProperty.Register(nameof(FileId),
            typeof(short),
            typeof(AllocationControl),
            null);

    public short FileId
    {
        get => (short)GetValue(FileIdProperty);
        set => SetValue(FileIdProperty, value);
    }

    public static readonly DependencyProperty IsTooltipEnabledProperty
        = DependencyProperty.Register(nameof(IsTooltipEnabled),
            typeof(bool),
            typeof(AllocationControl),
            null);

    public bool IsTooltipEnabled
    {
        get => (bool)GetValue(IsTooltipEnabledProperty);
        set => SetValue(IsTooltipEnabledProperty, value);
    }

    public static readonly DependencyProperty ExtentCountProperty
        = DependencyProperty.Register(nameof(ExtentCount),
            typeof(int),
            typeof(AllocationControl),
            new PropertyMetadata(null, OnPropertyChanged));

    public int ExtentCount
    {
        get => (int)GetValue(ExtentCountProperty);
        set => SetValue(ExtentCountProperty, value);
    }

    public static readonly DependencyProperty StartPageProperty
        = DependencyProperty.Register(nameof(StartPage),
            typeof(int),
            typeof(AllocationControl),
            new PropertyMetadata(null, OnPropertyChanged));

    /// <summary>
    /// Start page for use in allocation/PFS pages where the map is showing a discrete allocation bitmap rather than a chain
    /// </summary>
    public int StartPage
    {
        get => (int)GetValue(StartPageProperty);
        set => SetValue(StartPageProperty, value);
    }

    public static readonly DependencyProperty LayersProperty
        = DependencyProperty.Register(nameof(Layers),
                                      typeof(ObservableCollection<AllocationLayer>),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(null, OnPropertyChanged));

    public ObservableCollection<AllocationLayer> Layers
    {
        get => (ObservableCollection<AllocationLayer>)GetValue(LayersProperty);
        set => SetValue(LayersProperty, value);
    }

    public static readonly DependencyProperty SelectedLayersProperty
        = DependencyProperty.Register(nameof(SelectedLayers),
                                      typeof(ObservableCollection<AllocationLayer>),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(null, OnPropertyChanged));

    public ObservableCollection<AllocationLayer> SelectedLayers
    {
        get => (ObservableCollection<AllocationLayer>)GetValue(SelectedLayersProperty);
        set => SetValue(SelectedLayersProperty, value);
    }

    public static readonly DependencyProperty BordersProperty
        = DependencyProperty.Register(nameof(Borders),
                                      typeof(IReadOnlyList<AllocationBorder>),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(null, OnBordersChanged));

    public IReadOnlyList<AllocationBorder>? Borders
    {
        get => (IReadOnlyList<AllocationBorder>?)GetValue(BordersProperty);
        set => SetValue(BordersProperty, value);
    }

    public static readonly DependencyProperty SelectedRowIdentifierProperty
        = DependencyProperty.Register(nameof(SelectedRowIdentifier),
                                      typeof(RowIdentifier),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(null, OnPropertyChanged));

    public RowIdentifier? SelectedRowIdentifier
    {
        get => (RowIdentifier?)GetValue(SelectedRowIdentifierProperty);
        set => SetValue(SelectedRowIdentifierProperty, value);
    }

    public static readonly DependencyProperty SelectedRowSlotCountProperty
        = DependencyProperty.Register(nameof(SelectedRowSlotCount),
                                      typeof(int),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(0, OnPropertyChanged));

    public int SelectedRowSlotCount
    {
        get => (int)GetValue(SelectedRowSlotCountProperty);
        set => SetValue(SelectedRowSlotCountProperty, value);
    }

    public static readonly DependencyProperty PfsChainProperty
        = DependencyProperty.Register(nameof(PfsChain),
                                      typeof(PfsChain),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(null, OnPropertyChanged));

    public PfsChain PfsChain
    {
        get => (PfsChain)GetValue(PfsChainProperty);
        set => SetValue(PfsChainProperty, value);
    }

    public static readonly DependencyProperty IsPfsVisibleProperty
        = DependencyProperty.Register(nameof(IsPfsVisible),
                                      typeof(bool),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(null, OnPropertyChanged));

    public bool IsPfsVisible
    {
        get => (bool)GetValue(IsPfsVisibleProperty);
        set => SetValue(IsPfsVisibleProperty, value);
    }

    public static readonly DependencyProperty AutoScrollProperty
        = DependencyProperty.Register(nameof(AutoScroll),
                                      typeof(bool),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(false, OnAutoScrollChanged));

    public bool AutoScroll
    {
        get => (bool)GetValue(AutoScrollProperty);
        set => SetValue(AutoScrollProperty, value);
    }

    public static readonly DependencyProperty CurrentPageAddressProperty
        = DependencyProperty.Register(nameof(CurrentPageAddress),
                                      typeof(PageAddress?),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(null, OnCurrentPageAddressChanged));

    /// <summary>
    /// The page the map follows where its layers carry no timed spans
    /// </summary>
    /// <remarks>
    /// A trace draws the page it stands on as a border rather than as a span, so there is nothing under the playhead for the map to find
    /// and the page has to be given to it directly.
    /// </remarks>
    public PageAddress? CurrentPageAddress
    {
        get => (PageAddress?)GetValue(CurrentPageAddressProperty);
        set => SetValue(CurrentPageAddressProperty, value);
    }

    public static readonly DependencyProperty ZoomToCurrentPageProperty
        = DependencyProperty.Register(nameof(ZoomToCurrentPage),
                                      typeof(double?),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(null, OnZoomToCurrentPageChanged));

    /// <summary>
    /// The zoom to hold the followed page at, or null to leave the zoom to the user
    /// </summary>
    /// <remarks>
    /// Set, it follows as <see cref="AutoScroll"/> does, so a page far enough in to fill the map is still the one on screen.
    /// </remarks>
    public double? ZoomToCurrentPage
    {
        get => (double?)GetValue(ZoomToCurrentPageProperty);
        set => SetValue(ZoomToCurrentPageProperty, value);
    }

    public static readonly DependencyProperty IsHeatmapProperty
        = DependencyProperty.Register(nameof(IsHeatmap),
                                      typeof(bool),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(false, OnPropertyChanged));

    public bool IsHeatmap
    {
        get => (bool)GetValue(IsHeatmapProperty);
        set => SetValue(IsHeatmapProperty, value);
    }

    public static readonly DependencyProperty PlayheadTimeUsProperty
        = DependencyProperty.Register(nameof(PlayheadTimeUs),
                                      typeof(long),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(0L, OnPlayheadTimeChanged));

    public long PlayheadTimeUs
    {
        get => (long)GetValue(PlayheadTimeUsProperty);
        set => SetValue(PlayheadTimeUsProperty, value);
    }

    private static readonly DependencyProperty ZoomProperty
        = DependencyProperty.Register(nameof(Zoom),
                                      typeof(double),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(1D, OnPropertyChanged));

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    private readonly SKPaint _spanPaint = new();

    private readonly HashSet<int> _liveCells = [];

    private readonly SKPaint _overlayBorderPaint = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2,
        StrokeCap = SKStrokeCap.Square,
        IsAntialias = false,
    };

    private readonly Dictionary<PageAddress, (int Count, System.Drawing.Color Colour)> _heatmapVisits = new();

    private AllocationRenderer? _renderer;

    private SKPaint? _borderPaint;

    private Size _lastExtentSize;

    private AllocationBorder[] _orderedBorders = [];

    private Color _lastGridColor;

    private SKPicture? _staticLayer;

    private bool _isFollowingPage;

    private StaticLayerKey _staticLayerKey;

    private int _staticVersion;

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

        Loaded += (_, _) => Refresh();

        SetScrollBarValues();
    }

    public event EventHandler<PageAddressEventArgs>? PageClicked;

    public AllocationOverViewModel AllocationOver { get; } = new();

    /// <summary>
    /// The side of a page in pixels, which is a whole number so that the eight pages of an extent fill it exactly
    /// </summary>
    /// <remarks>
    /// A fractional width leaves a seam inside every extent, since each page is drawn as its own rectangle.
    /// </remarks>
    private int PageWidth => Math.Max(1, (int)Math.Round(UnitZoomPageWidth * Zoom));

    private Size ExtentSize => new(PageWidth * 8, PageWidth);

    private bool IsFollowingCurrentPage => AutoScroll || ZoomToCurrentPage is > 0;

    private ExtentLayout Layout { get; set; } = new();

    private int PageCount => ExtentCount * 8;

    private int ScrollPosition { get; set; }

    public void Dispose()
    {
        _staticLayer?.Dispose();
        _renderer?.Dispose();
        _borderPaint?.Dispose();
        _spanPaint.Dispose();
        _overlayBorderPaint.Dispose();

        AllocationCanvas.SizeChanged -= AllocationCanvas_SizeChanged;
        PointerWheelChanged -= AllocationControl_PointerWheelChanged;
        AllocationCanvas.PaintSurface -= AllocationCanvas_PaintSurface;
        AllocationCanvas.PointerMoved -= AllocationCanvas_PointerMoved;
        AllocationCanvas.PointerPressed -= AllocationCanvas_PointerPressed;
        AllocationCanvas.PointerExited -= AllocationCanvas_PointerExited;
        AllocationCanvas.PointerEntered -= AllocationCanvas_PointerEntered;

        if (Layers is { } layers)
        {
            layers.CollectionChanged -= OnLayersChanged;
        }

        if (SelectedLayers is { } selectedLayers)
        {
            selectedLayers.CollectionChanged -= OnSelectedLayersChanged;
        }
    }

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

    private void OnLayersChanged(object? sender,
                                 System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Refresh();

    private void OnSelectedLayersChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        // The static map depends on the data/layout that Refresh reacts to, so invalidate the cached picture.
        _staticVersion++;

        RebuildLayout((int)AllocationCanvas.ActualWidth, (int)AllocationCanvas.ActualHeight);

        if (IsFollowingCurrentPage)
        {
            FollowCurrentPage(PlayheadTimeUs);
        }

        AllocationCanvas.Invalidate();
    }

    /// <summary>
    /// Brings the page the map is following into view, zoomed in on it where a zoom is asked for
    /// </summary>
    /// <remarks>
    /// The span under the playhead and <see cref="CurrentPageAddress"/> are tried in turn rather than one standing for both, because an
    /// allocation map built for a trace carries no timed spans at all.
    /// </remarks>
    private void FollowCurrentPage(long playheadUs)
    {
        if (_isFollowingPage || Layout.HorizontalCount <= 0 || Layout.VisibleCount <= 0)
        {
            return;
        }

        var followed = LatestPageSpan(playheadUs)?.Address.PageId ?? FollowedPageId();

        if (followed is not { } pageId)
        {
            return;
        }

        _isFollowingPage = true;

        try
        {
            if (ZoomToCurrentPage is { } zoom)
            {
                ApplyZoom(zoom);
            }

            ScrollToPage(pageId);
        }
        finally
        {
            _isFollowingPage = false;
        }
    }

    private int? FollowedPageId()
        => CurrentPageAddress is { } address && address.FileId == FileId ? address.PageId : null;

    private PageSpan? LatestPageSpan(long playheadUs)
    {
        if (Layers is not { Count: > 0 })
        {
            return null;
        }

        PageSpan? latestSpan = null;

        foreach (var layer in Layers)
        {
            if (!layer.IsVisible || layer.Opacity == 0 || layer.PageSpans.Count == 0)
            {
                continue;
            }

            foreach (var span in layer.PageSpans)
            {
                if (span.Address.FileId != FileId || span.StartUs > playheadUs || span.EndUs < playheadUs)
                {
                    continue;
                }

                if (latestSpan is null
                    || span.StartUs > latestSpan.StartUs
                    || (span.StartUs == latestSpan.StartUs && span.EndUs >= latestSpan.EndUs))
                {
                    latestSpan = span;
                }
            }
        }

        return latestSpan;
    }

    /// <summary>
    /// Zooms the map, whose layout is rebuilt before the caller goes on to scroll against it
    /// </summary>
    private void ApplyZoom(double zoom)
    {
        var target = FitZoom(zoom);

        if (Math.Abs(Zoom - target) < 0.0001)
        {
            return;
        }

        Zoom = target;
    }

    /// <summary>
    /// The zoom near <paramref name="zoom"/> that leaves the least of the canvas untiled
    /// </summary>
    /// <remarks>
    /// Extents are laid out left to right until the next one will not fit, so a width the canvas is not a multiple of leaves a strip of
    /// dead space down the right hand side. Every page width within the tolerance is scored on what it would leave over, which is worth
    /// doing only where the zoom is chosen for the user - a zoom they set themselves is the one they asked for.
    /// </remarks>
    private double FitZoom(double zoom)
    {
        var target = Math.Clamp(zoom, MinimumZoom, MaximumZoom) * UnitZoomPageWidth;

        var lowest = Math.Max((int)Math.Ceiling(MinimumZoom * UnitZoomPageWidth), (int)Math.Round(target * (1 - FitZoomTolerance)));
        var highest = Math.Min((int)(MaximumZoom * UnitZoomPageWidth), (int)Math.Round(target * (1 + FitZoomTolerance)));

        var canvasWidth = (int)AllocationCanvas.ActualWidth;

        var fitted = Math.Clamp((int)Math.Round(target), lowest, Math.Max(lowest, highest));

        if (canvasWidth > 0)
        {
            var leastLeftOver = int.MaxValue;

            for (var width = lowest; width <= highest; width++)
            {
                var leftOver = canvasWidth % (width * 8);

                if (leftOver < leastLeftOver
                    || (leftOver == leastLeftOver && Math.Abs(width - target) < Math.Abs(fitted - target)))
                {
                    leastLeftOver = leftOver;
                    fitted = width;
                }
            }
        }

        return (double)fitted / UnitZoomPageWidth;
    }

    private void ScrollToPage(int pageId)
    {
        if (pageId < 0 || Layout.HorizontalCount <= 0 || Layout.VisibleCount <= 0)
        {
            return;
        }

        var targetExtent = pageId / 8;
        var firstVisible = ScrollPosition;
        var lastVisible = ScrollPosition + Layout.VisibleCount - 1;

        if (targetExtent >= firstVisible && targetExtent <= lastVisible)
        {
            return;
        }

        var horizontalCount = Layout.HorizontalCount;
        var centerOffset = Layout.VisibleCount / 2;
        var targetScroll = Math.Max(0, targetExtent - centerOffset);

        targetScroll -= targetScroll % horizontalCount;

        var maxStart = Math.Max(0, ExtentCount - Layout.VisibleCount);

        maxStart -= maxStart % horizontalCount;

        targetScroll = Math.Clamp(targetScroll, 0, maxStart);

        if ((int)ScrollBar.Value == targetScroll)
        {
            return;
        }

        ScrollBar.Value = targetScroll;
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
            var notches = e.GetCurrentPoint(this).Properties.MouseWheelDelta / 120D;

            ScrollBar.Value -= notches * ScrollBar.SmallChange * 3;
        }
    }

    private void AllocationCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RebuildLayout((int)e.NewSize.Width, (int)e.NewSize.Height);

        if (IsFollowingCurrentPage)
        {
            FollowCurrentPage(PlayheadTimeUs);
        }

        AllocationCanvas.Invalidate();
    }

    private void RebuildLayout(int width, int height)
    {
        Layout = GetExtentLayout(ExtentCount, ExtentSize, width, height);

        SetScrollBarValues();

        AlignScrollPosition();
    }

    private void SetScrollBarValues()
    {
        if (Layout.HorizontalCount == 0)
        {
            ScrollBar.IsEnabled = false;

            return;
        }

        var maxStart = Math.Max(0, ExtentCount - Layout.VisibleCount);

        maxStart -= maxStart % Layout.HorizontalCount;

        var lastRowStart = Math.Max(0, ExtentCount - 1) / Layout.HorizontalCount * Layout.HorizontalCount;

        ScrollBar.IsEnabled = ExtentCount > Layout.VisibleCount;
        ScrollBar.SmallChange = Layout.HorizontalCount;
        ScrollBar.LargeChange = Math.Max(1, Layout.VerticalCount - 1) * Layout.HorizontalCount;
        ScrollBar.Maximum = Math.Min(maxStart + ScrollBufferRows * Layout.HorizontalCount, lastRowStart);
        ScrollBar.ViewportSize = Layout.VisibleCount;
    }

    /// <summary>
    /// Re-align the scroll position to a row boundary of the current layout
    /// </summary>
    /// <remarks>
    /// The scroll position is held in extents but every position calculation assumes it starts a row, so a resize that
    /// changes the number of extents per row leaves it pointing part way into one.
    /// </remarks>
    private void AlignScrollPosition()
    {
        if (Layout.HorizontalCount == 0)
        {
            return;
        }

        var position = Math.Clamp((int)ScrollBar.Value, 0, (int)ScrollBar.Maximum);

        position -= position % Layout.HorizontalCount;

        ScrollPosition = position;

        if ((int)ScrollBar.Value != position)
        {
            ScrollBar.Value = position;
        }
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

        var key = new StaticLayerKey(ScrollPosition, e.Info.Width, e.Info.Height, _staticVersion);

        if (_staticLayer is null || !key.Equals(_staticLayerKey))
        {
            _staticLayer?.Dispose();
            _staticLayer = RecordStaticLayer(renderer, renderLayout, e.Info.Width, e.Info.Height);
            _staticLayerKey = key;
        }

        canvas.DrawPicture(_staticLayer);

        DrawPageActivity(canvas, renderLayout);

        // The grid is a single jagged H/V pass over the whole map (cheaper than stroking every cell), so it must sit
        // over the live page activity — drawn here rather than baked into the picture. Borders then paint over the grid.
        if (Zoom >= MinimumZoomForLines)
        {
            renderer.DrawPageLines(canvas, renderLayout.HorizontalCount, renderLayout.VerticalCount, renderLayout.RemainingCount);
        }

        DrawPageMarkerActivity(canvas, renderLayout);

        DrawBorders(canvas, renderLayout);

        DrawSelectedRow(canvas, renderLayout);
    }

    private void DrawSelectedRow(SKCanvas canvas, ExtentLayout layout)
    {
        if (SelectedRowIdentifier is not { } row
            || row.PageAddress.FileId != FileId
            || SelectedRowSlotCount <= 0
            || layout.HorizontalCount <= 0)
        {
            return;
        }

        var pageId = row.PageAddress.PageId - ScrollPosition * 8;

        if (pageId < 0)
        {
            return;
        }

        var rect = GetPagePosition(pageId, layout);

        if (rect.IsEmpty)
        {
            return;
        }

        var rowHeight = Math.Max(1F, rect.Height / SelectedRowSlotCount);

        var top = rect.Top + Math.Min(row.SlotId, SelectedRowSlotCount - 1) * rect.Height / SelectedRowSlotCount;

        using var paint = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Fill };

        canvas.DrawRect(new SKRect(rect.Left, top, rect.Right, top + rowHeight), paint);
    }

    private SKPicture RecordStaticLayer(AllocationRenderer renderer, ExtentLayout layout, int width, int height)
    {
        using var recorder = new SKPictureRecorder();

        var canvas = recorder.BeginRecording(new SKRect(0, 0, width, height));

        renderer.DrawBackgroundExtents(canvas, layout.HorizontalCount, layout.VerticalCount, layout.RemainingCount);

        DrawExtentMap(canvas, renderer, layout);

        if (IsPfsVisible)
        {
            using var pfsRenderer = new PfsRenderer(ExtentSize with { Width = PageWidth });

            DrawPfs(canvas, pfsRenderer, layout);
        }

        DrawPageMarkers(canvas, renderer, layout);

        if (SelectedLayers is { Count: > 0 })
        {
            foreach (var selectedLayer in SelectedLayers)
            {
                DrawScrollbarMarkers(canvas, Layout, selectedLayer, width, height);
            }
        }

        var mapWidth = layout.HorizontalCount * ExtentSize.Width;

        canvas.DrawLine(mapWidth, 0, mapWidth, height, _borderPaint);

        return recorder.EndRecording();
    }

    private void DrawScrollbarMarkers(SKCanvas canvas,
                                      ExtentLayout layout,
                                      AllocationLayer layer,
                                      int width,
                                      int height)
    {
        // Offset accounting for the scrollbar buttons
        const int offset = 18;

        // Size of each block next to the scrollbar
        const int blockSize = 4;

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
                    var top = offset + i * blockSize;
                    var bottom = offset + (i + 1) * blockSize;

                    var position = new SKRect(width - blockSize * 2, top, width, bottom);

                    canvas.DrawRect(position, paint);
                }
            }
        }
    }

    private void DrawExtentMap(SKCanvas canvas, AllocationRenderer renderer, ExtentLayout layout)
    {
        foreach (var layer in Layers)
        {
            if (!layer.IsVisible || layer.Opacity == 0)
            {
                continue;
            }

            var alpha = (byte)(layer.Opacity * 255 / 100);
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
                    renderer.DrawPage(canvas, GetPagePosition(page.PageId - (ScrollPosition * 8), layout), layer.LayerType);
                }
            }
        }
    }

    private void DrawPageActivity(SKCanvas canvas, ExtentLayout layout)
    {
        foreach (var layer in Layers)
        {
            if (!layer.IsVisible || layer.Opacity == 0)
            {
                continue;
            }

            if (IsHeatmap)
            {
                DrawPageSpanHeatmap(canvas, layout, layer);
            }
            else
            {
                DrawPageSpans(canvas, layout, layer);
            }
        }
    }

    private void DrawPageMarkerActivity(SKCanvas canvas, ExtentLayout layout)
    {
        foreach (var layer in Layers)
        {
            if (!layer.IsVisible || layer.Opacity == 0)
            {
                continue;
            }

            DrawPageMarkerSpans(canvas, layout, layer);
        }
    }

    private void DrawPageMarkers(SKCanvas canvas, AllocationRenderer renderer, ExtentLayout layout)
    {
        foreach (var layer in Layers)
        {
            if (!layer.IsVisible || layer.Opacity == 0)
            {
                continue;
            }

            foreach (var page in layer.SinglePages)
            {
                if (page.FileId == FileId)
                {
                    renderer.DrawPageMarker(canvas,
                                            GetPagePosition(page.PageId - (ScrollPosition * 8), layout),
                                            layer,
                                            layer.RendererColour);
                }
            }
        }
    }

    private void DrawBorders(SKCanvas canvas, ExtentLayout layout)
    {
        if (_orderedBorders.Length == 0 || layout.HorizontalCount <= 0)
        {
            return;
        }

        var playhead = PlayheadTimeUs;

        foreach (var border in _orderedBorders)
        {
            if (border.FileId != FileId)
            {
                continue;
            }

            var extentScope = border.Scope == AllocationBorderScope.Extent;

            var gridWidth = extentScope ? layout.HorizontalCount : layout.HorizontalCount * 8;

            var firstCell = extentScope ? ScrollPosition : ScrollPosition * 8;
            var lastCell = firstCell + (extentScope ? layout.VisibleCount : layout.VisibleCount * 8);

            var cells = _liveCells;

            cells.Clear();

            foreach (var range in border.Cells)
            {
                if (range.StartUs <= playhead && range.EndUs >= playhead)
                {
                    for (var cell = range.FromCell; cell <= range.ToCell; cell++)
                    {
                        cells.Add(cell);
                    }
                }
            }

            if (cells.Count == 0)
            {
                continue;
            }

            _overlayBorderPaint.Color = border.Colour.ToSkColor();

            foreach (var cell in cells)
            {
                if (cell < firstCell || cell >= lastCell)
                {
                    continue;
                }

                var rect = extentScope
                           ? GetExtentPosition(cell - firstCell, layout)
                           : GetPagePosition(cell - firstCell, layout);

                var column = (cell - firstCell) % gridWidth;

                var leftX = column == 0 ? rect.Left + 1 : rect.Left;
                var rightX = column == gridWidth - 1 ? rect.Right - 1 : rect.Right;

                if (column == 0 || !cells.Contains(cell - 1))
                {
                    canvas.DrawLine(leftX, rect.Top, leftX, rect.Bottom, _overlayBorderPaint);
                }

                if (column == gridWidth - 1 || !cells.Contains(cell + 1))
                {
                    canvas.DrawLine(rightX, rect.Top, rightX, rect.Bottom, _overlayBorderPaint);
                }

                if (!cells.Contains(cell - gridWidth))
                {
                    canvas.DrawLine(leftX, rect.Top, rightX, rect.Top, _overlayBorderPaint);
                }

                if (!cells.Contains(cell + gridWidth))
                {
                    canvas.DrawLine(leftX, rect.Bottom, rightX, rect.Bottom, _overlayBorderPaint);
                }
            }
        }
    }

    private static long BorderStartUs(AllocationBorder border) =>
        border.Cells.Count == 0 ? long.MaxValue : border.Cells.Min(c => c.StartUs);

    private void DrawPageSpans(SKCanvas canvas, ExtentLayout layout, AllocationLayer layer)
    {
        if (layer.LayerType != LayerType.Fill)
        {
            return;
        }

        var spans = layer.PageSpans;

        if (spans.Count == 0)
        {
            return;
        }

        var playhead = PlayheadTimeUs;

        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];

            if (span.StartUs > playhead)
            {
                break;
            }

            if (span.EndUs < playhead || span.Address.FileId != FileId)
            {
                continue;
            }

            _spanPaint.Color = (span.DisplayColour ?? layer.Colour).ToSkColor();

            canvas.DrawRect(GetPagePosition(span.Address.PageId - (ScrollPosition * 8), layout), _spanPaint);
        }
    }

    private void DrawPageMarkerSpans(SKCanvas canvas, ExtentLayout layout, AllocationLayer layer)
    {
        if (layer.LayerType == LayerType.Fill)
        {
            return;
        }

        var spans = layer.PageSpans;

        if (spans.Count == 0)
        {
            return;
        }

        var playhead = PlayheadTimeUs;

        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];

            if (span.StartUs > playhead)
            {
                break;
            }

            if (span.EndUs < playhead || span.Address.FileId != FileId)
            {
                continue;
            }

            _renderer?.DrawPageMarker(canvas,
                                      GetPagePosition(span.Address.PageId - (ScrollPosition * 8), layout),
                                      layer,
                                      span.DisplayColour?.ToSkColor() ?? layer.RendererColour);
        }
    }

    private void DrawPageSpanHeatmap(SKCanvas canvas, ExtentLayout layout, AllocationLayer layer)
    {
        var spans = layer.PageSpans;

        if (spans.Count == 0)
        {
            return;
        }

        var playhead = PlayheadTimeUs;

        _heatmapVisits.Clear();

        var maxCount = 0;

        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];

            if (span.StartUs > playhead)
            {
                break;
            }

            if (span.Address.FileId != FileId)
            {
                continue;
            }

            var colour = span.DisplayColour ?? layer.Colour;

            var count = _heatmapVisits.TryGetValue(span.Address, out var visit) ? visit.Count + 1 : 1;

            _heatmapVisits[span.Address] = (count, colour);

            if (count > maxCount)
            {
                maxCount = count;
            }
        }

        if (maxCount == 0)
        {
            return;
        }

        foreach (var (address, visit) in _heatmapVisits)
        {
            var position = GetPagePosition(address.PageId - (ScrollPosition * 8), layout);

            var ratio = (float)visit.Count / maxCount;

            _spanPaint.Color = GetHeatmapColour(visit.Colour, ratio).ToSkColor();

            canvas.DrawRect(position, _spanPaint);
        }
    }

    private static System.Drawing.Color GetHeatmapColour(System.Drawing.Color baseColour, float ratio)
    {
        var (l, c, h) = LchColorScale.LabToLch(LchColorScale.RgbToLab(baseColour));

        var chromaRatio = MinHeatmapChromaRatio + (1 - MinHeatmapChromaRatio) * ratio;

        return LchColorScale.LchToRgbSafe(l, c * chromaRatio, h);
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
            var toRender = false;

            for (var c = 0; c < chainCount; c++)
            {
                if (chains[c].IsExtentAllocated(i, fileId, isInverted))
                {
                    toRender = true;
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
        var horizontalCount = layout.HorizontalCount * 8;

        if (horizontalCount <= 0)
        {
            return SKRect.Empty;
        }

        var row = pageId / horizontalCount;
        var column = pageId % horizontalCount;

        var pageWidth = PageWidth;

        var left = column * pageWidth;
        var top = row * ExtentSize.Height;

        return new SKRect(left, top, left + pageWidth, top + ExtentSize.Height);
    }

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

        if (extentsHorizontal == 0 || rowsVisible == 0 || extentCount <= 0)
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

    private int GetExtentAtPosition(int x, int y)
    {
        var column = GetColumnAtPosition(x, ExtentSize.Width, Layout.HorizontalCount);

        return y / ExtentSize.Height * Layout.HorizontalCount + column + ScrollPosition;
    }

    private int GetPageAtPosition(int x, int y)
    {
        var column = GetColumnAtPosition(x, PageWidth, Layout.HorizontalCount * 8);

        return y / ExtentSize.Height * Layout.HorizontalCount * 8 + column + ScrollPosition * 8;
    }

    /// <summary>
    /// Get the column at a particular x position, clamped to the last column of the row
    /// </summary>
    /// <remarks>
    /// The column width has to match the fractional width the map is drawn at, otherwise the error against the drawn
    /// position accumulates across the row
    /// </remarks>
    private static int GetColumnAtPosition(int x, float columnWidth, int columnCount)
    {
        if (columnWidth <= 0 || columnCount <= 0)
        {
            return 0;
        }

        return Math.Clamp((int)(x / columnWidth), 0, columnCount - 1);
    }

    private void AllocationCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(this).Position;
        var canvasPosition = e.GetCurrentPoint(AllocationCanvas).Position;

        var pageId = GetPageAtPosition((int)canvasPosition.X, (int)canvasPosition.Y);
        var extentId = GetExtentAtPosition((int)canvasPosition.X, (int)canvasPosition.Y);

        var layer = Layers.FirstOrDefault(
            l => l.AllocationChains.Any(a => a.IsExtentAllocated(extentId, FileId, l.IsInverted))
                 || l.SinglePages.Any(p => p.PageId == pageId && p.FileId == FileId));

        var layerName = (pageId, FileId) switch
        {
            (0, _) => "File Header",
            (1, _) => "PFS",
            (2, _) => "GAM",
            (3, _) => "SGAM",
            (6, _) => "DCM",
            (7, _) => "BCM",
            (9, 1) => "Boot Page",
            _ => $"{layer?.Name ?? string.Empty}",
        };

        if (StartPage > 0)
        {
            var startExtent = StartPage / 8;

            AllocationOver.ExtentId = extentId + startExtent;
            AllocationOver.PageId = pageId + StartPage;
        }
        else
        {
            AllocationOver.ExtentId = extentId;
            AllocationOver.PageId = pageId;
        }

        AllocationOver.LayerName = layerName;
        AllocationOver.PfsValue = PfsChain?.GetPageStatus(pageId) ?? PfsByte.Unknown;

        if (IsTooltipEnabled)
        {
            TooltipPopup.HorizontalOffset = position.X + 5;
            TooltipPopup.VerticalOffset = position.Y + 5;

            if (!TooltipPopup.IsOpen)
            {
                TooltipPopup.IsOpen = true;
            }
        }
    }

    private void ScrollBar_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (Layout.HorizontalCount == 0)
        {
            return;
        }

        var scrollExtent = (int)ScrollBar.Value;

        ScrollPosition = scrollExtent - scrollExtent % Layout.HorizontalCount;

        AllocationCanvas.Invalidate();
    }

    private void AllocationCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(AllocationCanvas).Position;

        var pageId = GetPageAtPosition((int)position.X, (int)position.Y);

        if (pageId <= PageCount)
        {
            PageClicked?.Invoke(this, new PageAddressEventArgs(FileId, pageId, null));
        }
    }

    private void AllocationCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        TooltipPopup.IsOpen = false;
    }

    private void AllocationCanvas_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        //  AllocationOver.IsOpen = IsTooltipEnabled;
    }

    private readonly record struct StaticLayerKey(int ScrollPosition, int Width, int Height, int Version);

    private static void OnBordersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AllocationControl)d;

        control._orderedBorders = e.NewValue is IReadOnlyList<AllocationBorder> borders
            ? [.. borders.OrderBy(BorderStartUs)]
            : [];

        control.Refresh();
    }

    private static void OnPlayheadTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AllocationControl)d;
        var playheadUs = (long)e.NewValue;

        if (control.IsFollowingCurrentPage)
        {
            control.FollowCurrentPage(playheadUs);
        }

        if (control.Layers?.Any(l => l.PageSpans.Count > 0) == true
            || control.Borders is { Count: > 0 })
        {
            control.AllocationCanvas.Invalidate();
        }
    }

    private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AllocationControl)d;

        if ((bool)e.NewValue)
        {
            control.FollowCurrentPage(control.PlayheadTimeUs);
        }

        control.AllocationCanvas.Invalidate();
    }

    private static void OnZoomToCurrentPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AllocationControl)d;

        if (e.NewValue is > 0d)
        {
            control.FollowCurrentPage(control.PlayheadTimeUs);
        }

        control.AllocationCanvas.Invalidate();
    }

    private static void OnCurrentPageAddressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AllocationControl)d;

        if (control.IsFollowingCurrentPage)
        {
            control.FollowCurrentPage(control.PlayheadTimeUs);
        }
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