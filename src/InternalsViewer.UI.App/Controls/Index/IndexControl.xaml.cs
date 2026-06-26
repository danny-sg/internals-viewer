using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.System;
using Windows.UI.Core;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.UI.App.Controls.Allocation;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
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

    // Mirror of the Zoom dependency property. The size getters below are read hundreds of
    // thousands of times per frame in the draw loop; reading a field avoids a GetValue call
    // (which showed up as a hotspot in profiling) on every access. Kept in sync in OnPropertyChanged.
    private float _zoom = 1f;

    private float PageWidth => 20 * _zoom;
    private float PageHeight => 30 * _zoom;
    private float HorizontalMargin => 20 * _zoom;
    private float VerticalMargin => 60 * _zoom;

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
    private readonly SKPaint _shadowPaint;

    private readonly SKColor _backgroundColour = SKColors.White;
    private readonly SKColor _selectedBackgroundColour = SKColors.AliceBlue;
    private readonly SKColor _highlightedBackgroundColour = SKColors.Honeydew;

    private readonly SKColor _borderColour = SKColors.Gray;
    private readonly SKColor _selectedBorderColour = SKColors.Navy;
    private readonly SKColor _highlightedBorderColour = SKColors.Green;

    private readonly SKColor _lineColour = SKColors.Gray;
    private readonly SKColor _selectedLineColour = SKColors.Navy;

    private readonly List<IndexTreeNode> _nodePositions = [];

    private readonly Dictionary<int, List<IndexTreeNode>> _nodesByLevel = [];
    private readonly Dictionary<int, float> _levelMaxX = [];
    private readonly Dictionary<PageAddress, int> _ordinalByAddress = [];
    private float _globalMaxX;
    private int _levelCount;

    private readonly SKPath _linePath = new();

    private const float MinZoom = 0.2f;
    private const float MaxZoom = 3.0f;
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

        _shadowPaint = new SKPaint
        {

            Style = SKPaintStyle.Fill,
            Color = new SKColor(0, 0, 0, 70),
            IsAntialias = true,
            StrokeWidth = 1
        };

        float sigmaX = 5;
        float sigmaY = 5;

        _shadowPaint.ImageFilter = SKImageFilter.CreateBlur(sigmaX, sigmaY);

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
        }

        if (e.Property == ZoomProperty || e.Property == NodesProperty)
        {
            control.BuildIndexTree();
            control.UpdateScrollbars();
        }

        control.IndexCanvas.Invalidate();
    }

    private void IndexCanvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_nodePositions.Count == 0)
        {
            return;
        }

        e.Surface.Canvas.Clear(SKColors.Transparent);

        //// Draw levels from the bottom up
        for (var i = _levelCount; i >= 0; i--)
        {
            DrawTreeLevel(i, e.Surface.Canvas);
        }
    }

    private float GetNodeX(int n) 
        => (PageWidth + HorizontalMargin) * n;

    private float GetNodeY(int level, int row) 
        => PageHeight + VerticalMargin * level + (PageHeight + VerticalMargin * row);

    /// <summary>
    /// Build a virtual structure of the tree per level
    /// </summary>
    private void BuildIndexTree()
    {
        _nodePositions.Clear();
        _nodesByLevel.Clear();
        _levelMaxX.Clear();
        _ordinalByAddress.Clear();
        _globalMaxX = 0;
        _levelCount = 0;

        if (Nodes.Count == 0)
        {
            return;
        }

        _levelCount = Nodes.Max(n => n.Level);

        // Address -> ordinal lookup so DrawLines can resolve parents in O(1) rather than scanning.
        foreach (var node in Nodes)
        {
            _ordinalByAddress[node.PageAddress] = node.Ordinal;
        }

        for (var i = _levelCount; i >= 0; i--)
        {
            BuildIndexTreeLevel(i, Nodes);
        }

        // Group positions by level and record per-level / global extents once for the paint loop.
        foreach (var treeNode in _nodePositions)
        {
            var level = treeNode.Node.Level;

            if (!_nodesByLevel.TryGetValue(level, out var list))
            {
                list = [];
                _nodesByLevel[level] = list;
            }

            list.Add(treeNode);

            if (!_levelMaxX.TryGetValue(level, out var max) || treeNode.X > max)
            {
                _levelMaxX[level] = treeNode.X;
            }

            if (treeNode.X > _globalMaxX)
            {
                _globalMaxX = treeNode.X;
            }
        }
    }

    private void BuildIndexTreeLevel(int level, List<IndexNode> nodes)
    {
        var isFirstLevel = Nodes.Max(n => n.Level) == level;

        var verticalNodeCount = isFirstLevel ? 10 : 1;

        var levelNodes = nodes.Where(n => n.Level == level).ToList();

        var column = 1;
        var row = 1;

        IndexNode? previousNode = null;

        foreach (var node in levelNodes)
        {
            if (previousNode != null && !previousNode.Parents.SequenceEqual(node.Parents))
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

            var y = GetNodeY(level, row - 1);

            var x = GetNodeX(column - 1);

            _nodePositions.Add(new IndexTreeNode(node, x, y, row, column));

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

        var maxWidth = _globalMaxX + PageWidth + HorizontalMargin;
        var levelWidth = (_levelMaxX.GetValueOrDefault(level, 0)) + HorizontalMargin;

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
        var showDetail = _zoom > 0.5;

        foreach (var node in levelNodes)
        {
            var renderX = startX + node.X - xScrollOffset;

            var renderY = node.Y - yScrollOffset;

            // Only draw the page if it is visible
            if (clip.Contains(renderX, renderY))
            {
                var isHighlighted = highlightedAddresses.Contains(node.Node.PageAddress);
                var isSelected = node.Node.PageAddress == selectedAddress;

                DrawPage(canvas, renderX, renderY, isSelected, isHighlighted);

                if (showDetail)
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
            }

            DrawLines(canvas, clip, node.Node, renderX, renderY, renderNextLevelStartX, yScrollOffset, false, false);
        }
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
            _linePaint.Color = _selectedLineColour;
        }
        else
        {
            _linePaint.Color = _lineColour;
        }

        _linePaint.StrokeWidth = isSelected || isHighlighted ? 2.5f : 1f;
        _linePaint.StrokeJoin = SKStrokeJoin.Round;

        foreach (var parent in node.Parents)
        {
            if (!_ordinalByAddress.TryGetValue(parent, out var parentOrdinal))
            {
                continue;
            }

            var parentX = nextLevelStartX + GetNodeX(parentOrdinal);

            var y1Line1 = (float)Math.Floor(y + PageHeight / 2);

            var x2Line1 = (float)Math.Floor(x - HorizontalMargin / 2);

            var y2Line2 = (float)Math.Floor(GetNodeY(node.Level - 1, 0) 
                                            + PageHeight 
                                            + (VerticalMargin / 4f) 
                                            - yScrollOffset);

            var x2Line3 = (float)Math.Floor(parentX + (PageWidth / 2));

            var y2Line4 = (float)Math.Floor(GetNodeY(node.Level - 1, 0) + PageHeight - yScrollOffset);

           var lineLeft = Math.Min(x2Line1, x2Line3);
            var lineRight = Math.Max(x, x2Line3);

            if (lineRight < clip.Left || lineLeft > clip.Right || y1Line1 < clip.Top || y2Line4 > clip.Bottom)
            {
                continue;
            }

            _linePath.Reset();

            _linePath.MoveTo(x, y1Line1);
            _linePath.LineTo(x2Line1, y1Line1);
            _linePath.LineTo(x2Line1, y2Line2);
            _linePath.LineTo(x2Line3, y2Line2);
            _linePath.LineTo(x2Line3, y2Line4);

            canvas.DrawPath(_linePath, _linePaint);
        }
    }

    private void DrawPage(SKCanvas canvas,
                          float x,
                          float y,
                          bool isSelected,
                          bool isHighlighted)
    {
        var indexPageRect = new SKRect(x, y, x + PageWidth, y + PageHeight);

        _indexPagePaint.Style = SKPaintStyle.Stroke;

        if (isSelected)
        {
            _indexPagePaint.Color = _selectedBorderColour;
        }
        else if (isHighlighted)
        {
            _indexPagePaint.Color = _highlightedBorderColour;
        }
        else
        {
            _indexPagePaint.Color = _borderColour;
        }

        _indexPagePaint.StrokeWidth = isSelected || isHighlighted ? 2f : 1f;

        canvas.DrawRect(indexPageRect, _indexPagePaint);
    }

    /// <summary>
    /// Draws the lines horizontally indicating the index records
    /// </summary>
    private void DrawPageIndexDetail(SKCanvas canvas, float x, float y)
    {
        var verticalMargin = PageHeight / 6;
        var horizontalMargin = PageWidth * .1f;

        _indexPagePaint.Color = SKColors.LightGray;

        _indexPagePaint.StrokeWidth = 1;

        for (var i = 1; i < 6; i++)
        {
            canvas.DrawLine(x + horizontalMargin,
                            y + verticalMargin * i,
                            x + PageWidth - horizontalMargin,
                            y + verticalMargin * i,
                            _indexPagePaint);
        }
    }

    /// <summary>
    /// Draws the index lines vertically indicating the data record columns
    /// </summary>
    private void DrawPageDataDetail(SKCanvas canvas, float x, float y)
    {
        var verticalMargin = PageHeight * .1f;
        var horizontalMargin = PageWidth / 4;

        _indexPagePaint.Color = SKColors.LightGray;

        _indexPagePaint.StrokeWidth = 1;

        for (var i = 1; i < 4; i++)
        {
            canvas.DrawLine(x + horizontalMargin * i,
                            y + verticalMargin,
                            x + horizontalMargin * i,
                            y + PageHeight - verticalMargin,
                            _indexPagePaint);
        }
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

        var maxWidth = _globalMaxX + PageWidth + (HorizontalMargin * 2);
        var maxHeight = _nodePositions.Max(n => n.Y) + PageHeight + VerticalMargin;

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

    private void ScrollBar_OnScroll(object sender, ScrollEventArgs e)
    {
        IndexCanvas.Invalidate();
    }

    private void IndexControl_OnLoaded(object sender, RoutedEventArgs e)
    {
        // The Skia canvas goes blank after being reparented (tab re-selected). The tree layout is
        // unchanged, so just repaint from the cached positions - no need to rebuild or recompute
        // scrollbars (SizeChanged handles genuine size changes).
        IndexCanvas.Invalidate();
    }

    private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateScrollbars();
    }

    private void IndexCanvas_OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        TooltipPopup.IsOpen = false;
    }

    private void IndexCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
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

        var oldZoom = _zoom;
        var factor = delta > 0 ? 1.1f : 1f / 1.1f;
        var newZoom = Math.Clamp(oldZoom * factor, MinZoom, MaxZoom);

        e.Handled = true;

        if (Math.Abs(newZoom - oldZoom) < 0.0001f)
        {
            return;
        }

        // Preserve the scroll ratio so the viewport's centre stays roughly stable across the zoom
        var horizontalRatio = HorizontalScrollBar.Maximum > 0
            ? HorizontalScrollBar.Value / HorizontalScrollBar.Maximum
            : 0;

        var verticalRatio = VerticalScrollBar.Maximum > 0
            ? VerticalScrollBar.Value / VerticalScrollBar.Maximum
            : 0;

        // Setting Zoom rebuilds the tree and refreshes the scrollbar extents.
        Zoom = newZoom;

        HorizontalScrollBar.Value = HorizontalScrollBar.Maximum * horizontalRatio;
        VerticalScrollBar.Value = VerticalScrollBar.Maximum * verticalRatio;

        IndexCanvas.Invalidate();
    }

    private IndexNode? GetIndexNodeAtPosition(double x, double y)
    {
        var xScrollOffset = (float)HorizontalScrollBar.Value;
        var yScrollOffset = (float)VerticalScrollBar.Value;

        // Find the level first as the level offsets are used to center the tree.
        var level = _nodePositions.FirstOrDefault(n => y >= n.Y - yScrollOffset
                                                      && y <= n.Y - yScrollOffset + PageHeight)?.Node.Level;

        if (level is null)
        {
            return null;
        }

        var xOffset = GetLevelStartX(level.Value) - xScrollOffset;

        var node = _nodePositions.FirstOrDefault(n => x >= xOffset + n.X
                                                     && x <= xOffset + n.X + PageWidth
                                                     && y >= n.Y - yScrollOffset
                                                     && y <= n.Y - yScrollOffset + PageHeight);

        return node?.Node;
    }

    public void Dispose()
    {
        _indexPagePaint.Dispose();
        _linePaint.Dispose();
        _shadowPaint.Dispose();
        _linePath.Dispose();

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

public class ViewIndexEventArgs(long allocationUnitId) : EventArgs
{
    public long AllocationUnitId { get; } = allocationUnitId;
}

public sealed record IndexTreeNode(IndexNode Node, float X, float Y, int Row, int Column);