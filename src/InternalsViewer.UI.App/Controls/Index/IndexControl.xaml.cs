using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Windows.System;
using Windows.UI.Core;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.UI.App.Controls.Allocation;
using InternalsViewer.UI.App.Helpers;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace InternalsViewer.UI.App.Controls.Index;

public sealed partial class IndexControl : IDisposable
{
    public event EventHandler<PageAddressEventArgs>? PageClicked;

    public float Zoom
    {
        get => (float)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public static readonly DependencyProperty ZoomProperty
        = DependencyProperty.Register(nameof(Zoom),
                                      typeof(float),
                                      typeof(IndexControl),
                                      new PropertyMetadata(1F, OnPropertyChanged));

    public bool IsZoomToFit
    {
        get => (bool)GetValue(IsZoomToFitProperty);
        set => SetValue(IsZoomToFitProperty, value);
    }

    public static readonly DependencyProperty IsZoomToFitProperty
        = DependencyProperty.Register(nameof(IsZoomToFit),
                                      typeof(bool),
                                      typeof(IndexControl),
                                      new PropertyMetadata(true, OnPropertyChanged));

    public PageAddress? SelectedPageAddress
    {
        get => (PageAddress?)GetValue(SelectedPageAddressProperty);
        set => SetValue(SelectedPageAddressProperty, value);
    }

    public static readonly DependencyProperty SelectedPageAddressProperty
        = DependencyProperty.Register(nameof(SelectedPageAddress),
                                      typeof(PageAddress?),
                                      typeof(IndexControl),
                                      new PropertyMetadata(null, OnPropertyChanged));

    public int? SelectedSlot
    {
        get => (int?)GetValue(SelectedSlotProperty);
        set => SetValue(SelectedSlotProperty, value);
    }

    public static readonly DependencyProperty SelectedSlotProperty
        = DependencyProperty.Register(nameof(SelectedSlot),
            typeof(int?),
            typeof(IndexControl),
            new PropertyMetadata(null, OnPropertyChanged));

    /// <summary>
    /// Draws the lines to the selected page's children in the selection colour with a heavier stroke
    /// </summary>
    public bool SelectChildPath
    {
        get => (bool)GetValue(SelectChildPathProperty);
        set => SetValue(SelectChildPathProperty, value);
    }

    public static readonly DependencyProperty SelectChildPathProperty
        = DependencyProperty.Register(nameof(SelectChildPath),
                                      typeof(bool),
                                      typeof(IndexControl),
                                      new PropertyMetadata(true, OnPropertyChanged));

    public float? ZoomToSelectedPageAddress
    {
        get => (float?)GetValue(ZoomToSelectedPageAddressProperty);
        set => SetValue(ZoomToSelectedPageAddressProperty, value);
    }

    public static readonly DependencyProperty ZoomToSelectedPageAddressProperty
        = DependencyProperty.Register(nameof(ZoomToSelectedPageAddress),
                                      typeof(float?),
                                      typeof(IndexControl),
                                      new PropertyMetadata(null, OnPropertyChanged));

    public IReadOnlyList<PageSpan> PageSpans
    {
        get => (IReadOnlyList<PageSpan>)GetValue(PageSpansProperty);
        set => SetValue(PageSpansProperty, value);
    }

    public static readonly DependencyProperty PageSpansProperty
        = DependencyProperty.Register(nameof(PageSpans),
                                      typeof(IReadOnlyList<PageSpan>),
                                      typeof(IndexControl),
                                      new PropertyMetadata(null, OnPropertyChanged));

    /// <summary>
    /// Current playhead position (microseconds) - drives which spans are currently active
    /// </summary>
    public long PlayheadTimeUs
    {
        get => (long)GetValue(PlayheadTimeUsProperty);
        set => SetValue(PlayheadTimeUsProperty, value);
    }

    public static readonly DependencyProperty PlayheadTimeUsProperty
        = DependencyProperty.Register(nameof(PlayheadTimeUs),
                                      typeof(long),
                                      typeof(IndexControl),
                                      new PropertyMetadata(0L, OnPropertyChanged));

    public Windows.UI.Color SingleSelectedColour
    {
        get => (Windows.UI.Color)GetValue(SingleSelectedColourProperty);
        set => SetValue(SingleSelectedColourProperty, value);
    }

    public static readonly DependencyProperty SingleSelectedColourProperty
        = DependencyProperty.Register(nameof(SingleSelectedColour),
                                      typeof(Windows.UI.Color),
                                      typeof(IndexControl),
                                      new PropertyMetadata(Microsoft.UI.Colors.Navy, OnPropertyChanged));


    public Windows.UI.Color SelectedSlotColour
    {
        get => (Windows.UI.Color)GetValue(SelectedSlotColourProperty);
        set => SetValue(SelectedSlotColourProperty, value);
    }

    public static readonly DependencyProperty SelectedSlotColourProperty
        = DependencyProperty.Register(nameof(SelectedSlotColour),
            typeof(Windows.UI.Color),
            typeof(IndexControl),
            new PropertyMetadata(Microsoft.UI.Colors.Red, OnPropertyChanged));

    public Windows.UI.Color SelectedBackgroundColour
    {
        get => (Windows.UI.Color)GetValue(SelectedBackgroundColourProperty);
        set => SetValue(SelectedBackgroundColourProperty, value);
    }

    public static readonly DependencyProperty SelectedBackgroundColourProperty
        = DependencyProperty.Register(nameof(SelectedBackgroundColour),
            typeof(Windows.UI.Color),
            typeof(IndexControl),
            new PropertyMetadata(Microsoft.UI.Colors.White, OnPropertyChanged));

    public Windows.UI.Color RangeSelectedColour
    {
        get => (Windows.UI.Color)GetValue(RangeSelectedColourProperty);
        set => SetValue(RangeSelectedColourProperty, value);
    }

    public static readonly DependencyProperty RangeSelectedColourProperty
        = DependencyProperty.Register(nameof(RangeSelectedColour),
                                      typeof(Windows.UI.Color),
                                      typeof(IndexControl),
                                      new PropertyMetadata(Microsoft.UI.Colors.Navy, OnPropertyChanged));

    private IndexNode? HoverNode
    {
        get => (IndexNode?)GetValue(HoverNodeProperty);
        set => SetValue(HoverNodeProperty, value);
    }

    private static readonly DependencyProperty HoverNodeProperty
        = DependencyProperty.Register(nameof(HoverNode),
            typeof(IndexNode),
            typeof(IndexControl),
            new PropertyMetadata(null, OnPropertyChanged));

    public ObservableCollection<PageAddress> HighlightedPageAddresses
    {
        get => (ObservableCollection<PageAddress>)GetValue(HighlightedPageAddressesProperty);
        set => SetValue(HighlightedPageAddressesProperty, value);
    }

    public static readonly DependencyProperty HighlightedPageAddressesProperty
        = DependencyProperty.Register(nameof(HighlightedPageAddresses),
            typeof(ObservableCollection<PageAddress>),
            typeof(IndexControl),
            new PropertyMetadata(null, OnPropertyChanged));

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

    private float _zoom = 1f;

    private bool _isZoomToFit = true;

    // True only while a fit-driven zoom is being applied, so that self-triggered zoom change is not mistaken for a
    // manual one (which would switch fit off).
    private bool _applyingFit;

    // Fit the content a touch inside the viewport; the slack stops the scrollbars flickering on at the exact boundary
    // (a full-bleed fit can leave content == viewport, which flips a scrollbar on, shrinking the viewport, and so on).
    private const float FitPadding = 0.95f;

    private const float ZoomMiniMode = 0.8f;
    private const float ZoomMaxiMode = 4f;

    private float PageWidth => 20 * _zoom;
    private float PageHeight => 30 * _zoom;
    private float HorizontalMargin => 20 * _zoom;
    private float VerticalMargin => 60 * _zoom;
    private float LevelMargin => 90 * _zoom;

    public List<IndexNode> Nodes
    {
        get => (List<IndexNode>)GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    public static readonly DependencyProperty NodesProperty
        = DependencyProperty.Register(nameof(Nodes),
                                      typeof(List<IndexNode>),
                                      typeof(IndexControl),
                                      new PropertyMetadata(new(), OnPropertyChanged));

    private readonly SKPaint _indexPagePaint;
    private readonly SKPaint _linePaint;
    private readonly SKPaint _detailTextPaint;
    private readonly SKPaint _slotPaint;

    private readonly SKFont _detailFont = new(SKTypeface.Default, 10f);
    private readonly SKFont _detailBoldFont = new(SKTypeface.FromFamilyName(SKTypeface.Default.FamilyName, SKFontStyle.Bold), 10f);

    private readonly SKColor _borderColour = SKColors.Gray;
    private readonly SKColor _highlightedBorderColour = SKColors.Green;
    private readonly SKColor _lineColour = SKColors.DarkGray;
    private readonly SKColor _miniColour = SKColors.LightGray;

    private SKColor _singleSelectedColour = SKColors.Navy;
    private SKColor _rangeSelectedColour = SKColors.Navy;

    private readonly Dictionary<PageAddress, SKColor> _activeSpanColours = [];

    private readonly List<IndexTreeNode> _nodePositions = [];

    private readonly Dictionary<int, List<IndexTreeNode>> _nodesByLevel = [];

    private readonly Dictionary<int, int> _levelMaxColumn = [];

    private readonly Dictionary<int, int> _levelMaxRow = [];

    private readonly Dictionary<int, int> _levelMaxColumnAfterParent = [];

    private readonly Dictionary<int, int> _levelMaxColumnBeforeParent = [];
    private int _globalMaxColumn;

    private readonly Dictionary<PageAddress, int> _ordinalByAddress = [];
    private int _levelCount;

    private readonly Dictionary<PageAddress, IndexTreeNode> _treeNodeByAddress = [];

    private const float ZoomToPageDurationMs = 450f;

    private readonly Stopwatch _zoomToPageStopwatch = new();

    private bool _isZoomToPageRunning;
    private PageAddress _zoomToPageTargetAddress;
    private float _zoomToPageStartZoom;
    private float _zoomToPageTargetZoom;
    private float _zoomToPageStartLookAtX;
    private float _zoomToPageStartLookAtY;

    private float NodeX(IndexTreeNode node) => GetNodeX(node.Column - 1);

    private float NodeY(IndexTreeNode node) => GetNodeY(node.Node.Level, node.Row - 1);

    private readonly SKPoint[] _linePoints = new SKPoint[5];

    private const float MinZoom = 0.05f;
    private const float MaxZoom = 10.0f;
    private const double DragThreshold = 4;

    private bool _isPointerDown;
    private bool _isDragging;
    private Windows.Foundation.Point _dragStart;
    private double _dragStartHorizontal;
    private double _dragStartVertical;

    public IndexControl()
    {
        InitializeComponent();

        IndexCanvas.PaintSurface += IndexCanvas_PaintSurface;
        IndexCanvas.PointerMoved += IndexCanvas_PointerMoved;
        IndexCanvas.PointerExited += IndexCanvas_OnPointerExited;
        IndexCanvas.PointerPressed += IndexCanvas_PointerPressed;
        IndexCanvas.PointerReleased += IndexCanvas_PointerReleased;
        IndexCanvas.PointerCaptureLost += IndexCanvas_PointerReleased;
        IndexCanvas.PointerWheelChanged += IndexCanvas_PointerWheelChanged;

        Loaded += IndexControl_OnLoaded;

        _indexPagePaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.Gray,
            IsAntialias = true,
            StrokeWidth = 1
        };

        _linePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.IndianRed,
            IsAntialias = false,
            StrokeWidth = 1f
        };

        _detailTextPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.Navy,
            IsAntialias = true
        };

        _slotPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SelectedSlotColour.ToSKColor(),
            IsAntialias = false,
            StrokeWidth = 1f,
            StrokeCap = SKStrokeCap.Square
        };
    }

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (IndexControl)d;

        if (e.Property == HoverNodeProperty)
        {
            return;
        }

        if (e.Property == ZoomProperty)
        {
            control._zoom = (float)e.NewValue;

            if (!control._applyingFit)
            {
                control.IsZoomToFit = false;
            }
        }

        if (e.Property == IsZoomToFitProperty)
        {
            control._isZoomToFit = (bool)e.NewValue;
        }

        if (e.Property == NodesProperty)
        {
            control.BuildIndexTree();
        }

        if (e.Property == ZoomProperty)
        {
            control.UpdateScrollbarsCentredOnZoom((float)e.OldValue, (float)e.NewValue);
        }
        else if (e.Property == NodesProperty)
        {
            control.UpdateScrollbars();
        }

        if (e.Property == NodesProperty || (e.Property == IsZoomToFitProperty && control._isZoomToFit))
        {
            control.ApplyZoomToFit();
        }

        if (e.Property == ZoomToSelectedPageAddressProperty ||
            (e.Property == SelectedPageAddressProperty && control.ZoomToSelectedPageAddress is > 0))
        {
            control.StartZoomToSelectedPage();
        }

        control.IndexCanvas.Invalidate();
    }

    /// <summary>
    /// When fit mode is on, sets <see cref="Zoom"/> so the whole tree fits the viewport
    /// </summary>
    /// <remarks>
    /// Every layout dimension scales linearly with the zoom, so the fitting zoom is the current zoom scaled by the smaller of the width/
    /// height viewport-to-content ratios.
    ///
    /// Setting <see cref="Zoom"/> rebuilds and repaints.
    /// </remarks>
    private void ApplyZoomToFit()
    {
        if (!_isZoomToFit || _nodePositions.Count == 0)
        {
            return;
        }

        var canvasWidth = (float)IndexCanvas.ActualWidth;

        var canvasHeight = (float)IndexCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            return;
        }

        var contentWidth = GetNodeX(_globalMaxColumn - 1) + PageWidth + HorizontalMargin * 2;

        var contentHeight = GetMaxNodeY() + PageHeight + VerticalMargin;

        if (contentWidth <= 0 || contentHeight <= 0)
        {
            return;
        }

        var fit = _zoom * Math.Min(canvasWidth / contentWidth, canvasHeight / contentHeight) * FitPadding;

        fit = Math.Clamp(fit, MinZoom, MaxZoom);

        if (Math.Abs(fit - _zoom) < 0.0001f)
        {
            return;
        }

        _applyingFit = true;

        Zoom = fit;

        _applyingFit = false;
    }

    private void IndexCanvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_nodePositions.Count == 0)
        {
            return;
        }

        e.Surface.Canvas.Clear(SKColors.Transparent);

        _singleSelectedColour = SingleSelectedColour.ToSkColor();
        _rangeSelectedColour = RangeSelectedColour.ToSkColor();

        _activeSpanColours.Clear();

        CollectActiveSpanColours(PageSpans, PlayheadTimeUs, _rangeSelectedColour, _activeSpanColours);

        // Draw levels from the bottom up
        for (var i = _levelCount; i >= 0; i--)
        {
            DrawTreeLevel(i, e.Surface.Canvas);
        }
    }

    private static void CollectActiveSpanColours(IReadOnlyList<PageSpan>? spans,
                                                 long playhead,
                                                 SKColor defaultColour,
                                                 Dictionary<PageAddress, SKColor> into)
    {
        if (spans is null)
        {
            return;
        }

        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];

            if (span.StartUs > playhead)
            {
                break;
            }

            if (span.EndUs < playhead)
            {
                continue;
            }

            into[span.Address] = span.DisplayColour?.ToSkColor() ?? defaultColour;
        }
    }

    private float GetNodeX(int n)
        => (PageWidth + HorizontalMargin) * n;

    private float GetNodeY(int level, int row)
        => PageHeight + LevelMargin * level + (PageHeight + VerticalMargin * row);

    private float GetMaxNodeY()
    {
        var max = 0f;

        foreach (var (level, maxRow) in _levelMaxRow)
        {
            var y = GetNodeY(level, maxRow - 1);

            if (y > max)
            {
                max = y;
            }
        }

        return max;
    }

    /// <summary>
    /// Build a virtual structure of the tree per level
    /// </summary>
    private void BuildIndexTree()
    {
        _nodePositions.Clear();
        _nodesByLevel.Clear();
        _levelMaxColumn.Clear();
        _levelMaxRow.Clear();
        _levelMaxColumnAfterParent.Clear();
        _levelMaxColumnBeforeParent.Clear();
        _ordinalByAddress.Clear();
        _treeNodeByAddress.Clear();

        _globalMaxColumn = 0;
        _levelCount = 0;

        if (Nodes.Count == 0)
        {
            return;
        }

        _levelCount = Nodes.Max(n => n.Level);

        foreach (var node in Nodes)
        {
            _ordinalByAddress[node.PageAddress] = node.Ordinal;
        }

        for (var i = _levelCount; i >= 0; i--)
        {
            BuildIndexTreeLevel(i, Nodes);
        }

        foreach (var treeNode in _nodePositions)
        {
            _treeNodeByAddress[treeNode.Node.PageAddress] = treeNode;

            var level = treeNode.Node.Level;

            if (!_nodesByLevel.TryGetValue(level, out var list))
            {
                list = [];
                _nodesByLevel[level] = list;
            }

            list.Add(treeNode);

            if (!_levelMaxColumn.TryGetValue(level, out var max) || treeNode.Column > max)
            {
                _levelMaxColumn[level] = treeNode.Column;
            }

            if (!_levelMaxRow.TryGetValue(level, out var maxRow) || treeNode.Row > maxRow)
            {
                _levelMaxRow[level] = treeNode.Row;
            }

            var parentOrdinal = _ordinalByAddress.GetValueOrDefault(treeNode.Node.Parent);

            var columnAfterParent = treeNode.Column - parentOrdinal;

            if (!_levelMaxColumnAfterParent.TryGetValue(level, out var maxAfter) || columnAfterParent > maxAfter)
            {
                _levelMaxColumnAfterParent[level] = columnAfterParent;
            }

            if (!_levelMaxColumnBeforeParent.TryGetValue(level, out var maxBefore) || -columnAfterParent > maxBefore)
            {
                _levelMaxColumnBeforeParent[level] = -columnAfterParent;
            }

            if (treeNode.Column > _globalMaxColumn)
            {
                _globalMaxColumn = treeNode.Column;
            }
        }
    }

    private void BuildIndexTreeLevel(int level, List<IndexNode> nodes)
    {
        var isFirstLevel = Nodes.Max(n => n.Level) == level;

        const int leafPagesPerColumn = 20;

        var verticalNodeCount = isFirstLevel ? leafPagesPerColumn : 1;

        var levelNodes = nodes.Where(n => n.Level == level).ToList();

        var column = 1;
        var row = 1;

        IndexNode? previousNode = null;

        foreach (var node in levelNodes)
        {
            if (previousNode != null)
            {
                if (previousNode.Parent != node.Parent)
                {
                    // Start a new column, leaving a gap of a column as the parent node has changed
                    row = 1;
                    column += 2;
                }
                else if (row % verticalNodeCount == 0)
                {
                    // Start a new column
                    row = 1;
                    column++;
                }
                else
                {
                    // Move to the next row
                    row++;
                }
            }

            _nodePositions.Add(new IndexTreeNode(node, row, column));

            previousNode = node;
        }
    }

    /// <summary>
    /// Gets the X offset for the start of the level
    /// </summary>
    /// <remarks>
    /// Nodes are created in two phases, the build which is a one-off, and the draw which is performed on every
    /// re-render.
    /// 
    /// The centering can change depending on the window size to the X offsets are calculated as part of the draw.
    /// 
    /// The build phase starts each level at X = 0, e.g.
    /// 
    ///     Level 0 |----|
    ///     Level 1 |-----------------|
    ///     Level 2 |------------------------|
    ///     
    /// The offset is calculated for each level based on maximum width less the level width, divided by 2. These
    /// offsets center the tree:
    /// 
    ///     Level 0          |----|
    ///     Level 1    |-----------------|
    ///     Level 2 |------------------------|
    /// </remarks>
    private float GetLevelStartX(int level)
    {
        if (level < 0)
        {
            return 0;
        }

        var canvasWidth = IndexCanvas.ActualSize.X;

        var maxWidth = GetNodeX(_globalMaxColumn - 1) + PageWidth + HorizontalMargin;
        var levelWidth = GetNodeX(_levelMaxColumn.GetValueOrDefault(level, 1) - 1) + HorizontalMargin;

        if (maxWidth < canvasWidth)
        {
            return (canvasWidth - levelWidth) / 2;
        }

        return HorizontalMargin + (maxWidth - levelWidth) / 2;
    }

    private void DrawTreeLevel(int level, SKCanvas canvas)
    {
        var xScrollOffset = (float)HorizontalScrollBar.Value;
        var yScrollOffset = (float)VerticalScrollBar.Value;

        if (!_nodesByLevel.TryGetValue(level, out var levelNodes))
        {
            return;
        }

        var startX = GetLevelStartX(level);
        var nextLevelStartX = GetLevelStartX(level - 1);

        // X position of the next level used to draw lines from the page to the parent
        var renderNextLevelStartX = (nextLevelStartX - xScrollOffset);

        var clip = canvas.LocalClipBounds;

        // Snapshot the dependency-property reads once per level instead of per node.
        var selectedAddress = SelectedPageAddress;
        var highlightedAddresses = HighlightedPageAddresses;

        var miniMode = _zoom < ZoomMiniMode;
        var maxiMode = _zoom > ZoomMaxiMode;

        var stride = PageWidth + HorizontalMargin;

        var verticalMargin = VerticalMargin;

        var levelBaseY = PageHeight + (LevelMargin * level) + PageHeight;

        var minColumn = (int)Math.Floor((clip.Left + xScrollOffset - startX) / stride) + 1;
        var maxColumn = (int)Math.Ceiling((clip.Right + xScrollOffset - startX) / stride) + 1;

        if (!miniMode)
        {
            var maxLineLeft = (stride * _levelMaxColumnBeforeParent.GetValueOrDefault(level))
                              + nextLevelStartX - startX;

            var maxLineRight = (stride * _levelMaxColumnAfterParent.GetValueOrDefault(level))
                               + startX - nextLevelStartX;

            if (maxLineLeft > 0)
            {
                minColumn -= (int)Math.Ceiling(maxLineLeft / stride);
            }

            if (maxLineRight > 0)
            {
                maxColumn += (int)Math.Ceiling(maxLineRight / stride);
            }
        }

        var startIndex = FindFirstColumnIndex(levelNodes, minColumn);

        for (var i = startIndex; i < levelNodes.Count; i++)
        {
            var node = levelNodes[i];

            if (node.Column > maxColumn)
            {
                break;
            }

            var renderX = startX + (stride * (node.Column - 1)) - xScrollOffset;

            var renderY = levelBaseY + (verticalMargin * (node.Row - 1)) - yScrollOffset;

            // Only draw the page if it is visible
            if (clip.Contains(renderX, renderY))
            {
                var isHighlighted = highlightedAddresses?.Contains(node.Node.PageAddress) ?? false;
                var isSelected = node.Node.PageAddress == selectedAddress;
                var hasSpanColour = _activeSpanColours.TryGetValue(node.Node.PageAddress, out var spanColour);

                if (miniMode)
                {
                    DrawMiniPage(canvas, renderX, renderY, isSelected, isHighlighted, hasSpanColour, spanColour);
                }
                else
                {
                    DrawPage(canvas, renderX, renderY, isSelected, isHighlighted, hasSpanColour, spanColour);

                    if (!maxiMode)
                    {
                        if (node.Node is { PageType: PageType.Data })
                        {
                            DrawPageDataDetail(canvas, renderX, renderY);

                        }
                        else
                        {
                            DrawPageIndexDetail(canvas, renderX, renderY);
                        }
                    }
                    else
                    {
                        var headerHeight = DrawFullPageDetail(canvas, node.Node, renderX, renderY);

                        if (node.Node is { PageType: PageType.Data })
                        {
                            DrawPageDataDetail(canvas, renderX, renderY, headerHeight);
                        }
                        else
                        {
                            DrawPageIndexDetail(canvas, renderX, renderY, headerHeight);
                        }
                    }
                }

                if (SelectedPageAddress == node.Node.PageAddress && SelectedSlot != null)
                {
                    DrawSelectedSlot(canvas, node.Node, renderX, renderY, 1);
                }
            }

            if (!miniMode)
            {
                DrawLines(canvas,
                          clip,
                          node.Node,
                          renderX,
                          renderY,
                          renderNextLevelStartX,
                          yScrollOffset,
                          SelectChildPath && node.Node.Parent == selectedAddress,
                          false);
            }
        }
    }

    private void DrawSelectedSlot(SKCanvas canvas, IndexNode nodeNode, float renderX, float renderY, int borderWidth)
    {
        var y = renderY + ((PageHeight / nodeNode.SlotCount) * SelectedSlot ?? 0);

        _slotPaint.StrokeWidth = 2;

        canvas.DrawLine(renderX + borderWidth, y, renderX + borderWidth + PageWidth - (borderWidth * 2), y, _slotPaint);
    }

    private static int FindFirstColumnIndex(List<IndexTreeNode> levelNodes, int minColumn)
    {
        var low = 0;

        var high = levelNodes.Count;

        while (low < high)
        {
            var mid = (low + high) / 2;

            if (levelNodes[mid].Column < minColumn)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    /// <summary>
    /// Draws line(s) to parent node(s)
    /// </summary>
    private void DrawLines(SKCanvas canvas,
                           SKRect clip,
                           IndexNode node,
                           float x,
                           float y,
                           float nextLevelStartX,
                           float yScrollOffset,
                           bool isSelected,
                           bool isHighlighted)
    {
        //            X         Parent Node 
        //            | Line 4
        //      ------  Line 3  
        //      |       Line 2
        //      --X     Line 1  Node    
        if (isHighlighted)
        {
            _linePaint.Color = _highlightedBorderColour;
        }
        else if (isSelected)
        {
            _linePaint.Color = _singleSelectedColour;
        }
        else
        {
            _linePaint.Color = _lineColour;
        }

        _linePaint.StrokeWidth = isSelected || isHighlighted ? 2.5f : 1f;

        // The polyline is drawn as individual segments (DrawPoints), so rounded corners come from the
        // cap rather than the join.
        _linePaint.StrokeCap = SKStrokeCap.Round;

        if (!_ordinalByAddress.TryGetValue(node.Parent, out var parentOrdinal))
        {
            return;
        }

        var parentX = nextLevelStartX + GetNodeX(parentOrdinal - 1);

        var y1Line1 = (float)Math.Floor(y + PageHeight / 2);

        var x2Line1 = (float)Math.Floor(x - HorizontalMargin / 2);

        var y2Line2 = (float)Math.Floor(GetNodeY(node.Level - 1, 0)
                                        + PageHeight
                                        + ((LevelMargin - PageHeight) / 2f)
                                        - yScrollOffset);

        var x2Line3 = (float)Math.Floor(parentX + (PageWidth / 2));

        var y2Line4 = (float)Math.Floor(GetNodeY(node.Level - 1, 0) + PageHeight - yScrollOffset);

        var lineLeft = Math.Min(x2Line1, x2Line3);
        var lineRight = Math.Max(x, x2Line3);

        if (lineRight < clip.Left || lineLeft > clip.Right || y1Line1 < clip.Top || y2Line4 > clip.Bottom)
        {
            return;
        }

        _linePoints[0] = new SKPoint(x, y1Line1);
        _linePoints[1] = new SKPoint(x2Line1, y1Line1);
        _linePoints[2] = new SKPoint(x2Line1, y2Line2);
        _linePoints[3] = new SKPoint(x2Line3, y2Line2);
        _linePoints[4] = new SKPoint(x2Line3, y2Line4);

        canvas.DrawPoints(SKPointMode.Polygon, _linePoints, _linePaint);
    }

    private void DrawPage(SKCanvas canvas,
                          float x,
                          float y,
                          bool isSelected,
                          bool isHighlighted,
                          bool hasSpanColour,
                          SKColor spanColour)
    {
        var indexPageRect = new SKRect(x, y, x + PageWidth, y + PageHeight);

        if (isSelected || hasSpanColour)
        {
            // Draw selected background
            _indexPagePaint.Style = SKPaintStyle.Fill;
            _indexPagePaint.Color = _singleSelectedColour;

            canvas.DrawRect(indexPageRect, _indexPagePaint);
        }

        _indexPagePaint.Style = SKPaintStyle.Stroke;

        if (isSelected)
        {
            _indexPagePaint.Color = _singleSelectedColour;
        }
        else if (isHighlighted)
        {
            _indexPagePaint.Color = _highlightedBorderColour;
        }
        else if (hasSpanColour)
        {
            _indexPagePaint.Color = spanColour;
        }
        else
        {
            _indexPagePaint.Color = _borderColour;
        }

        _indexPagePaint.Style = SKPaintStyle.Stroke;
        _indexPagePaint.StrokeWidth = isHighlighted ? 2f : 1f;

        canvas.DrawRect(indexPageRect, _indexPagePaint);
    }

    private void DrawMiniPage(SKCanvas canvas,
                              float x,
                              float y,
                              bool isSelected,
                              bool isHighlighted,
                              bool hasSpanColour,
                              SKColor spanColour)
    {
        var indexPageRect = new SKRect(x, y, x + PageWidth, y + PageHeight);

        _indexPagePaint.Style = SKPaintStyle.Fill;

        if (isSelected)
        {
            _indexPagePaint.Color = _singleSelectedColour;
        }
        else if (isHighlighted)
        {
            _indexPagePaint.Color = _highlightedBorderColour;
        }
        else if (hasSpanColour)
        {
            _indexPagePaint.Color = spanColour;
        }
        else
        {
            _indexPagePaint.Color = _miniColour;
        }

        canvas.DrawRoundRect(indexPageRect, 1, 1, _indexPagePaint);
    }

    /// <summary>
    /// Draws the lines horizontally indicating the index records
    /// </summary>
    private void DrawPageIndexDetail(SKCanvas canvas, float x, float y, float yOffset = 0)
    {
        // yOffset shifts the detail below the page header (maxi-mode); the remaining height is divided up.
        var availableHeight = PageHeight - yOffset;
        var top = y + yOffset;

        var verticalMargin = availableHeight / 6;
        var horizontalMargin = PageWidth * .1f;

        _indexPagePaint.Style = SKPaintStyle.Stroke;
        _indexPagePaint.Color = SKColors.LightGray;

        _indexPagePaint.StrokeWidth = 1;

        for (var i = 1; i < 6; i++)
        {
            canvas.DrawLine(x + horizontalMargin,
                            top + verticalMargin * i,
                            x + PageWidth - horizontalMargin,
                            top + verticalMargin * i,
                            _indexPagePaint);
        }
    }

    /// <summary>
    /// Draws the index lines vertically indicating the data record columns
    /// </summary>
    private void DrawPageDataDetail(SKCanvas canvas, float x, float y, float yOffset = 0)
    {
        // yOffset shifts the detail below the page header (maxi-mode); the remaining height is divided up.
        var availableHeight = PageHeight - yOffset;
        var top = y + yOffset;

        var verticalMargin = availableHeight * .1f;
        var horizontalMargin = PageWidth / 4;

        _indexPagePaint.Style = SKPaintStyle.Stroke;
        _indexPagePaint.Color = SKColors.LightGray;

        _indexPagePaint.StrokeWidth = 1;

        for (var i = 1; i < 4; i++)
        {
            canvas.DrawLine(x + horizontalMargin * i,
                            top + verticalMargin,
                            x + horizontalMargin * i,
                            top + availableHeight - verticalMargin,
                            _indexPagePaint);
        }
    }

    /// <summary>
    /// Draws the page header when zoomed in far enough (maxi-mode) to show readable text.
    /// </summary>
    /// <remarks>
    /// The header occupies the top two rows of the page:
    ///
    ///     --------------------------
    ///     |      Page Address      |
    ///     |------------------------|
    ///     |  Previous |    Next    |
    ///     |------------------------|
    ///
    /// The detail lines below the header are drawn offset by the returned header height.
    /// </remarks>
    /// <returns>The height of the header, used to offset the detail drawn below it.</returns>
    private float DrawFullPageDetail(SKCanvas canvas, IndexNode node, float x, float y)
    {
        var rowHeight = PageHeight / 6;

        var addressRowBottom = y + rowHeight;
        var linkRowBottom = y + rowHeight * 2;
        var midX = x + PageWidth / 2;

        _indexPagePaint.Style = SKPaintStyle.Stroke;
        _indexPagePaint.Color = _borderColour;
        _indexPagePaint.StrokeWidth = 1;

        // Horizontal divider beneath the page address
        canvas.DrawLine(x, addressRowBottom, x + PageWidth, addressRowBottom, _indexPagePaint);

        // Vertical divider splitting Previous | Next
        canvas.DrawLine(midX, addressRowBottom, midX, linkRowBottom, _indexPagePaint);

        // Horizontal divider beneath Previous | Next
        canvas.DrawLine(x, linkRowBottom, x + PageWidth, linkRowBottom, _indexPagePaint);

        // Page address, centred and bold across the full width
        DrawCellText(canvas, node.PageAddress.ToString(), x, y, PageWidth, rowHeight, bold: true);

        // Previous and Next addresses in their half-width cells, padded from the edges
        DrawCellText(canvas, node.PreviousPage.ToString(), x, addressRowBottom, PageWidth / 2, rowHeight, horizontalPadding: 6f);
        DrawCellText(canvas, node.NextPage.ToString(), midX, addressRowBottom, PageWidth / 2, rowHeight, horizontalPadding: 6f);

        return rowHeight * 2;
    }

    /// <summary>
    /// Draws text centred within a cell, shrinking the font so it fits the cell width.
    /// </summary>
    private void DrawCellText(SKCanvas canvas,
                              string text,
                              float cellX, float cellY, float cellWidth, float cellHeight,
                              bool bold = false,
                              float horizontalPadding = 0f)
    {
        var font = bold ? _detailBoldFont : _detailFont;

        var availableWidth = cellWidth - horizontalPadding * 2;

        // Start from a font sized to the cell height, then shrink further if the text is too wide.
        font.Size = cellHeight * 0.6f;

        var textWidth = font.MeasureText(text);

        if (textWidth > availableWidth)
        {
            font.Size *= availableWidth / textWidth;
        }

        var baseline = cellY + cellHeight / 2 + font.Size * 0.35f;

        canvas.DrawText(text, cellX + cellWidth / 2, baseline, SKTextAlign.Center, font, _detailTextPaint);
    }

    private void UpdateScrollbars()
    {
        if (_nodePositions.Count == 0)
        {
            HorizontalScrollBar.Visibility = Visibility.Collapsed;
            HorizontalScrollBar.Maximum = 0;
            HorizontalScrollBar.Value = 0;
            VerticalScrollBar.Visibility = Visibility.Collapsed;
            VerticalScrollBar.Maximum = 0;
            VerticalScrollBar.Value = 0;
            return;
        }

        var maxWidth = GetNodeX(_globalMaxColumn - 1) + PageWidth + (HorizontalMargin * 2);

        var maxHeight = _nodePositions.Max(NodeY) + PageHeight + VerticalMargin;

        if (maxWidth < IndexCanvas.ActualWidth)
        {
            HorizontalScrollBar.Visibility = Visibility.Collapsed;
            HorizontalScrollBar.Maximum = 0;
            HorizontalScrollBar.Value = 0;
        }
        else
        {
            var previousValue = HorizontalScrollBar.Maximum;

            HorizontalScrollBar.Visibility = Visibility.Visible;
            HorizontalScrollBar.Maximum = maxWidth - IndexCanvas.ActualWidth;

            if (previousValue <= 1)
            {
                HorizontalScrollBar.Value = HorizontalScrollBar.Maximum / 2;
            }
            else
            {
                HorizontalScrollBar.Value = Math.Min(HorizontalScrollBar.Value, HorizontalScrollBar.Maximum);
            }
        }

        if (maxHeight < IndexCanvas.ActualHeight)
        {
            VerticalScrollBar.Visibility = Visibility.Collapsed;
            VerticalScrollBar.Maximum = 0;
            VerticalScrollBar.Value = 0;
        }
        else
        {
            VerticalScrollBar.Visibility = Visibility.Visible;
            VerticalScrollBar.Maximum = maxHeight - IndexCanvas.ActualHeight;
            VerticalScrollBar.Value = Math.Min(VerticalScrollBar.Value, VerticalScrollBar.Maximum);
        }
    }

    private void UpdateScrollbarsCentredOnZoom(float oldZoom, float newZoom)
    {
        var ratio = oldZoom > 0 ? newZoom / oldZoom : 1f;

        var wasHorizontalScrollable = HorizontalScrollBar.Maximum > 0;

        var horizontalCentre = (HorizontalScrollBar.Value + IndexCanvas.ActualWidth / 2) * ratio;

        UpdateScrollbars();

        if (wasHorizontalScrollable && HorizontalScrollBar.Maximum > 0)
        {
            HorizontalScrollBar.Value = Math.Clamp(horizontalCentre - IndexCanvas.ActualWidth / 2,
                                                   0,
                                                   HorizontalScrollBar.Maximum);
        }
    }

    private void StartZoomToSelectedPage()
    {
        var targetZoom = ZoomToSelectedPageAddress;
        var address = SelectedPageAddress;

        if (targetZoom is not > 0 || address is null || !_treeNodeByAddress.ContainsKey(address.Value))
        {
            StopZoomToSelectedPage();

            return;
        }

        _zoomToPageTargetAddress = address.Value;
        _zoomToPageTargetZoom = Math.Clamp(targetZoom.Value, MinZoom, MaxZoom);
        _zoomToPageStartZoom = _zoom;

        _zoomToPageStartLookAtX = (float)((HorizontalScrollBar.Value + IndexCanvas.ActualWidth / 2) / _zoom);
        _zoomToPageStartLookAtY = (float)((VerticalScrollBar.Value + IndexCanvas.ActualHeight / 2) / _zoom);

        _zoomToPageStopwatch.Restart();

        if (!_isZoomToPageRunning)
        {
            _isZoomToPageRunning = true;

            CompositionTarget.Rendering += ZoomToPage_Rendering;
        }
    }

    private void ZoomToPage_Rendering(object? sender, object e)
    {
        if (!_treeNodeByAddress.TryGetValue(_zoomToPageTargetAddress, out var target))
        {
            StopZoomToSelectedPage();

            return;
        }

        var t = Math.Clamp(_zoomToPageStopwatch.ElapsedMilliseconds / ZoomToPageDurationMs, 0f, 1f);

        var eased = t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;

        Zoom = _zoomToPageStartZoom + (_zoomToPageTargetZoom - _zoomToPageStartZoom) * eased;

        var targetLookAtX = (GetLevelStartX(target.Node.Level) + GetNodeX(target.Column - 1) + PageWidth / 2) / _zoom;
        var targetLookAtY = (NodeY(target) + PageHeight / 2) / _zoom;

        var lookAtX = (_zoomToPageStartLookAtX + (targetLookAtX - _zoomToPageStartLookAtX) * eased) * _zoom;
        var lookAtY = (_zoomToPageStartLookAtY + (targetLookAtY - _zoomToPageStartLookAtY) * eased) * _zoom;

        if (HorizontalScrollBar.Maximum > 0)
        {
            HorizontalScrollBar.Value = Math.Clamp(lookAtX - IndexCanvas.ActualWidth / 2, 0, HorizontalScrollBar.Maximum);
        }

        if (VerticalScrollBar.Maximum > 0)
        {
            VerticalScrollBar.Value = Math.Clamp(lookAtY - IndexCanvas.ActualHeight / 2, 0, VerticalScrollBar.Maximum);
        }

        IndexCanvas.Invalidate();

        if (t >= 1f)
        {
            StopZoomToSelectedPage();
        }
    }

    private void StopZoomToSelectedPage()
    {
        if (_isZoomToPageRunning)
        {
            _isZoomToPageRunning = false;

            CompositionTarget.Rendering -= ZoomToPage_Rendering;
        }
    }

    private void ScrollBar_OnScroll(object sender, ScrollEventArgs e)
    {
        IndexCanvas.Invalidate();
    }

    private void IndexControl_OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyZoomToFit();

        IndexCanvas.Invalidate();
    }

    private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateScrollbars();

        ApplyZoomToFit();
    }

    private void IndexCanvas_OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        TooltipPopup.IsOpen = false;
    }

    private void IndexCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        StopZoomToSelectedPage();

        var position = e.GetCurrentPoint(this).Position;

        _isPointerDown = true;
        _isDragging = false;
        _dragStart = position;
        _dragStartHorizontal = HorizontalScrollBar.Value;
        _dragStartVertical = VerticalScrollBar.Value;

        IndexCanvas.CapturePointer(e.Pointer);
    }

    private void IndexCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isPointerDown && !_isDragging)
        {
            // A press with no drag is a click - select the node under the pointer.
            HandleClick(e.GetCurrentPoint(this).Position);
        }

        if (_isDragging)
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        }

        _isPointerDown = false;
        _isDragging = false;

        IndexCanvas.ReleasePointerCapture(e.Pointer);
    }

    private void HandleClick(Windows.Foundation.Point position)
    {
        var node = GetIndexNodeAtPosition(position.X, position.Y);

        SelectedPageAddress = node?.PageAddress;

        IndexCanvas.Invalidate();

        if (node is not null)
        {
            var state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);

            var isShiftPressed = state.HasFlag(CoreVirtualKeyStates.Down);

            PageClicked?.Invoke(this, new PageAddressEventArgs(node.PageAddress.FileId, node.PageAddress.PageId)
            { Tag = isShiftPressed ? "Open" : string.Empty });
        }
        else
        {
            PageClicked?.Invoke(this, new PageAddressEventArgs(PageAddress.Empty));
        }
    }

    /// <summary>
    /// Pans the view while dragging, otherwise tracks the hovered node to drive the tooltip.
    /// </summary>
    private void IndexCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(this).Position;

        if (_isPointerDown)
        {
            var deltaX = position.X - _dragStart.X;
            var deltaY = position.Y - _dragStart.Y;

            if (!_isDragging && (Math.Abs(deltaX) > DragThreshold || Math.Abs(deltaY) > DragThreshold))
            {
                _isDragging = true;
                TooltipPopup.IsOpen = false;
                HoverNode = null;
                ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
            }

            if (_isDragging)
            {
                Pan(_dragStartHorizontal - deltaX, _dragStartVertical - deltaY);
            }

            return;
        }

        var node = GetIndexNodeAtPosition(position.X, position.Y);

        if (node is not null)
        {
            HoverNode = node;

            TooltipPopup.HorizontalOffset = position.X + 10;
            TooltipPopup.VerticalOffset = position.Y + 10;
            TooltipPopup.IsOpen = true;
        }
        else
        {
            TooltipPopup.IsOpen = false;
            HoverNode = null;
        }
    }

    private void Pan(double horizontal, double vertical)
    {
        if (HorizontalScrollBar.Maximum > 0)
        {
            HorizontalScrollBar.Value = Math.Clamp(horizontal, 0, HorizontalScrollBar.Maximum);
        }

        if (VerticalScrollBar.Maximum > 0)
        {
            VerticalScrollBar.Value = Math.Clamp(vertical, 0, VerticalScrollBar.Maximum);
        }

        IndexCanvas.Invalidate();
    }

    private void IndexCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(IndexCanvas).Properties.MouseWheelDelta;

        if (delta == 0)
        {
            return;
        }

        StopZoomToSelectedPage();

        var oldZoom = _zoom;
        var factor = delta > 0 ? 1.1f : 1f / 1.1f;
        var newZoom = Math.Clamp(oldZoom * factor, MinZoom, MaxZoom);

        e.Handled = true;

        if (Math.Abs(newZoom - oldZoom) < 0.0001f)
        {
            return;
        }

        // Setting Zoom rebuilds the tree and refreshes the scrollbar extents.
        Zoom = newZoom;

        IndexCanvas.Invalidate();
    }

    private IndexNode? GetIndexNodeAtPosition(double x, double y)
    {
        if (_nodePositions.Count == 0)
        {
            return null;
        }

        var xScrollOffset = (float)HorizontalScrollBar.Value;
        var yScrollOffset = (float)VerticalScrollBar.Value;

        var pageHeight = PageHeight;
        var verticalMargin = VerticalMargin;

        // Find the level first as the level offsets are used to center the tree.
        var worldY = y + yScrollOffset - (pageHeight * 2);

        if (worldY < 0)
        {
            return null;
        }

        int level;
        int row;

        var levelBand = (int)Math.Floor(worldY / LevelMargin);

        if (levelBand < _levelCount)
        {
            if (worldY - (levelBand * LevelMargin) > pageHeight)
            {
                return null;
            }

            level = levelBand;
            row = 1;
        }
        else
        {
            var leafY = worldY - (LevelMargin * _levelCount);

            var rowBand = (int)Math.Floor(leafY / verticalMargin);

            if (leafY - (rowBand * verticalMargin) > pageHeight)
            {
                return null;
            }

            level = _levelCount;
            row = rowBand + 1;
        }

        var worldX = x + xScrollOffset - GetLevelStartX(level);

        if (worldX < 0)
        {
            return null;
        }

        var stride = PageWidth + HorizontalMargin;

        var column = (int)Math.Floor(worldX / stride) + 1;

        if (worldX - ((column - 1) * stride) > PageWidth)
        {
            return null;
        }

        if (!_nodesByLevel.TryGetValue(level, out var levelNodes))
        {
            return null;
        }

        var index = FindFirstColumnIndex(levelNodes, column);

        for (var i = index; i < levelNodes.Count && levelNodes[i].Column == column; i++)
        {
            if (levelNodes[i].Row == row)
            {
                return levelNodes[i].Node;
            }
        }

        return null;
    }

    public void Dispose()
    {
        StopZoomToSelectedPage();

        _indexPagePaint.Dispose();
        _linePaint.Dispose();
        _detailTextPaint.Dispose();
        _detailFont.Dispose();
        _detailBoldFont.Dispose();
        _slotPaint.Dispose();

        Loaded -= IndexControl_OnLoaded;

        IndexCanvas.PaintSurface -= IndexCanvas_PaintSurface;
        IndexCanvas.PointerMoved -= IndexCanvas_PointerMoved;
        IndexCanvas.PointerExited -= IndexCanvas_OnPointerExited;
        IndexCanvas.PointerPressed -= IndexCanvas_PointerPressed;
        IndexCanvas.PointerReleased -= IndexCanvas_PointerReleased;
        IndexCanvas.PointerCaptureLost -= IndexCanvas_PointerReleased;
        IndexCanvas.PointerWheelChanged -= IndexCanvas_PointerWheelChanged;
    }
}