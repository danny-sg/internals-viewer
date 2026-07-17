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

namespace InternalsViewer.UI.App.Controls.Allocation;

public sealed partial class AllocationControl : IDisposable
{
    private const double MinimumZoom = 0.2;
    private const double MaximumZoom = 4;

    private const double MinimumZoomForLines = 0.4;

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
            new PropertyMetadata(null, OnPropertyChanged));

    public Color GridColor
    {
        get => (Color)GetValue(GridColorProperty);
        set => SetValue(GridColorProperty, value);
    }

    public static readonly DependencyProperty GridColorProperty
        = DependencyProperty.Register(nameof(GridColor),
            typeof(Color),
            typeof(AllocationControl),
            new PropertyMetadata(null, OnPropertyChanged));

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

    public int ExtentCount
    {
        get => (int)GetValue(ExtentCountProperty);
        set => SetValue(ExtentCountProperty, value);
    }

    public static readonly DependencyProperty ExtentCountProperty
        = DependencyProperty.Register(nameof(ExtentCount),
                                     typeof(int),
                                     typeof(AllocationControl),
                                     new PropertyMetadata(null, OnPropertyChanged));

    public ObservableCollection<AllocationLayer> Layers
    {
        get => (ObservableCollection<AllocationLayer>)GetValue(LayersProperty);
        set => SetValue(LayersProperty, value);
    }

    public static readonly DependencyProperty LayersProperty
        = DependencyProperty.Register(nameof(Layers),
                                      typeof(ObservableCollection<AllocationLayer>),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(null, OnPropertyChanged));

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

    // Cell-group outlines drawn over the map (a Tetris-piece perimeter per group). The caller supplies the cell ranges
    // and colour; the control draws the borders (see DrawBorders). Locks are the first use — see AllocationBorder.
    public IReadOnlyList<AllocationBorder>? Borders
    {
        get => (IReadOnlyList<AllocationBorder>?)GetValue(BordersProperty);
        set => SetValue(BordersProperty, value);
    }

    public static readonly DependencyProperty BordersProperty
        = DependencyProperty.Register(nameof(Borders),
                                      typeof(IReadOnlyList<AllocationBorder>),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(null, OnBordersChanged));

    // Paint order (earliest hold first, so a later lock draws over one already held) resolved once here rather than per
    // paint — neither the order nor a border's start changes between frames, but DrawBorders runs on every playhead tick.
    private static void OnBordersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AllocationControl)d;

        control._orderedBorders = e.NewValue is IReadOnlyList<AllocationBorder> borders
            ? [.. borders.OrderBy(BorderStartUs)]
            : [];

        control.Refresh();
    }

    public PfsChain PfsChain
    {
        get => (PfsChain)GetValue(PfsChainProperty);
        set => SetValue(PfsChainProperty, value);
    }

    public static readonly DependencyProperty PfsChainProperty
        = DependencyProperty.Register(nameof(PfsChain),
                                      typeof(PfsChain),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(null, OnPropertyChanged));

    public bool IsPfsVisible
    {
        get => (bool)GetValue(IsPfsVisibleProperty);
        set => SetValue(IsPfsVisibleProperty, value);
    }

    public static readonly DependencyProperty IsPfsVisibleProperty
        = DependencyProperty.Register(nameof(IsPfsVisible),
                                      typeof(bool),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(null, OnPropertyChanged));

    public bool AutoScroll
    {
        get => (bool)GetValue(AutoScrollProperty);
        set => SetValue(AutoScrollProperty, value);
    }

    public static readonly DependencyProperty AutoScrollProperty
        = DependencyProperty.Register(nameof(AutoScroll),
                                      typeof(bool),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(false, OnAutoScrollChanged));

    public bool IsHeatmap
    {
        get => (bool)GetValue(IsHeatmapProperty);
        set => SetValue(IsHeatmapProperty, value);
    }

    public static readonly DependencyProperty IsHeatmapProperty
        = DependencyProperty.Register(nameof(IsHeatmap),
                                      typeof(bool),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(false, OnPropertyChanged));

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public long PlayheadTimeUs
    {
        get => (long)GetValue(PlayheadTimeUsProperty);
        set => SetValue(PlayheadTimeUsProperty, value);
    }

    public static readonly DependencyProperty PlayheadTimeUsProperty
        = DependencyProperty.Register(nameof(PlayheadTimeUs),
                                      typeof(long),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(0L, OnPlayheadTimeChanged));

    private static void OnPlayheadTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AllocationControl)d;
        var playheadUs = (long)e.NewValue;

        if (control.AutoScroll)
        {
            control.ScrollToLatestPageSpan(playheadUs);
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
            control.ScrollToLatestPageSpan(control.PlayheadTimeUs);
        }

        control.AllocationCanvas.Invalidate();
    }

    private static readonly DependencyProperty ZoomProperty
        = DependencyProperty.Register(nameof(Zoom),
                                      typeof(double),
                                      typeof(AllocationControl),
                                      new PropertyMetadata(1D, OnPropertyChanged));

    public AllocationOverViewModel AllocationOver { get; } = new();

    private int PageCount => ExtentCount * 8;

    private AllocationRenderer? _renderer;
    private SKPaint? _borderPaint;
    private Size _lastExtentSize;

    private readonly SKPaint _spanPaint = new();

    // Borders in paint order, rebuilt only when the Borders property changes (see OnBordersChanged).
    private AllocationBorder[] _orderedBorders = [];

    // Reused across borders and frames: DrawBorders repopulates it per border on every playhead tick.
    private readonly HashSet<int> _liveCells = [];

    // Stroke for lock-border outlines (the colour is set per border). Crisp, square edges: no antialiasing so the 2px
    // lines land on whole pixels, and square caps so the separately-drawn edges meet cleanly at corners.
    private readonly SKPaint _overlayBorderPaint = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2,
        StrokeCap = SKStrokeCap.Square,
        IsAntialias = false,
    };

    private Color _lastGridColor;

    // The allocation map (background/extents/PFS/lines/markers/scrollbar/border) is recorded into a picture once and
    // replayed each paint; only the playhead-driven page spans and lock borders are redrawn live. Re-recorded only when
    // the map itself changes — see StaticLayerKey and _staticVersion — so a playhead tick just replays and draws spans.
    private SKPicture? _staticLayer;
    private StaticLayerKey _staticLayerKey;

    // Bumped whenever a change to the static map goes through Refresh (layers, colours, zoom, PFS, selection). Scroll and
    // resize don't call Refresh but are caught by the key's own fields, so a playhead tick alone leaves the key unchanged.
    private int _staticVersion;

    private readonly record struct StaticLayerKey(int ScrollPosition, int Width, int Height, int Version);

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

        Loaded += (_, _) => Refresh();

        SetScrollBarValues();
    }

    private void Refresh()
    {
        // The static map depends on the data/layout that Refresh reacts to, so invalidate the cached picture.
        _staticVersion++;

        Layout = GetExtentLayout(ExtentCount,
                                 ExtentSize,
                                 (int)AllocationCanvas.ActualWidth,
                                 (int)AllocationCanvas.ActualHeight);

        SetScrollBarValues();

        if (AutoScroll)
        {
            ScrollToLatestPageSpan(PlayheadTimeUs);
        }

        AllocationCanvas.Invalidate();
    }

    private void ScrollToLatestPageSpan(long playheadUs)
    {
        if (Layers is not { Count: > 0 } || Layout.HorizontalCount <= 0 || Layout.VisibleCount <= 0)
        {
            return;
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

        if (latestSpan is null)
        {
            return;
        }

        ScrollToPage(latestSpan.Address.PageId);
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

        DrawBorders(canvas, renderLayout);
    }

    private SKPicture RecordStaticLayer(AllocationRenderer renderer, ExtentLayout layout, int width, int height)
    {
        using var recorder = new SKPictureRecorder();

        var canvas = recorder.BeginRecording(new SKRect(0, 0, width, height));

        renderer.DrawBackgroundExtents(canvas, layout.HorizontalCount, layout.VerticalCount, layout.RemainingCount);

        DrawExtentMap(canvas, renderer, layout);

        if (IsPfsVisible)
        {
            using var pfsRenderer = new PfsRenderer(ExtentSize with { Width = ExtentSize.Width / 8 });

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
                    var top = offset + i * blockSize;
                    var bottom = offset + (i + 1) * blockSize;

                    var position = new SKRect(width - blockSize * 2, top, width, bottom);

                    canvas.DrawRect(position, paint);
                }
            }
        }
    }

    // The static allocation map per layer: the allocated extents (chains) and any single pages. The playhead-driven page
    // spans are drawn separately by DrawPageActivity, so they can be redrawn without re-recording this.
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

    // The playhead-driven page spans (or heatmap) per layer — the reads that have happened up to the current playhead
    // time. Drawn live over the cached map on every paint.
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
                    renderer.DrawPageMarker(canvas, GetPagePosition(page.PageId - (ScrollPosition * 8), layout), layer.LayerType);
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

                // The on-screen column, derived the same way the rect is positioned (cell - firstCell), NOT the absolute
                // cell. A resize changes gridWidth without re-aligning firstCell to it, so cell % gridWidth would place a
                // cell's map-edge trim on a different column than its rect and the outline edges land wrong.
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

    // A border's paint order key: the earliest hold across its ranges (empty sorts last, though it is skipped anyway).
    private static long BorderStartUs(AllocationBorder border) =>
        border.Cells.Count == 0 ? long.MaxValue : border.Cells.Min(c => c.StartUs);

    private void DrawPageSpans(SKCanvas canvas, ExtentLayout layout, AllocationLayer layer)
    {
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

    private readonly Dictionary<PageAddress, (int Count, System.Drawing.Color Colour)> _heatmapVisits = new();

    private const float MinHeatmapChromaRatio = 0.15F;

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

        var pageWidth = ExtentSize.Width / 8F;

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
            // Position before opening so the popup never appears for a frame at its previous/zero offset.
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
        TooltipPopup.IsOpen = false;
    }

    private void AllocationCanvas_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
      //  AllocationOver.IsOpen = IsTooltipEnabled;
    }

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