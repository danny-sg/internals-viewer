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

    private float PageWidth => 20 * Zoom;
    private float PageHeight => 30 * Zoom;
    private float HorizontalMargin => 20 * Zoom;
    private float VerticalMargin => 60 * Zoom;

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

    private readonly SKPaint indexPagePaint;
    private readonly SKPaint linePaint;
    private readonly SKPaint shadowPaint;

    private readonly SKColor backgroundColour = SKColors.White;
    private readonly SKColor selectedBackgroundColour = SKColors.AliceBlue;
    private readonly SKColor highlightedBackgroundColour = SKColors.Honeydew;

    private readonly SKColor borderColour = SKColors.Gray;
    private readonly SKColor selectedBorderColour = SKColors.Navy;
    private readonly SKColor highlightedBorderColour = SKColors.Green;

    private readonly SKColor lineColour = SKColors.Gray;
    private readonly SKColor selectedLineColour = SKColors.Navy;

    private readonly List<IndexTreeNode> nodePositions = [];

    // Caches rebuilt only when the tree changes (Nodes/Zoom), so the per-frame paint avoids
    // repeated O(N) LINQ scans and O(N) parent lookups.
    private readonly Dictionary<int, List<IndexTreeNode>> nodesByLevel = [];
    private readonly Dictionary<int, float> levelMaxX = [];
    private readonly Dictionary<PageAddress, int> ordinalByAddress = [];
    private float globalMaxX;
    private int levelCount;

    // Reused across line draws to avoid allocating an SKPath per parent on every frame.
    private readonly SKPath linePath = new();

    private const float MinZoom = 0.2f;
    private const float MaxZoom = 3.0f;
    private const double DragThreshold = 4;

    private bool isPointerDown;
    private bool isDragging;
    private Windows.Foundation.Point dragStart;
    private double dragStartHorizontal;
    private double dragStartVertical;

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

        shadowPaint = new SKPaint
        {

            Style = SKPaintStyle.Fill,
            Color = new SKColor(0, 0, 0, 70),
            IsAntialias = true,
            StrokeWidth = 1
        };

        float sigmaX = 5;
        float sigmaY = 5;

        shadowPaint.ImageFilter = SKImageFilter.CreateBlur(sigmaX, sigmaY);

        indexPagePaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.Gray,
            IsAntialias = true,
            StrokeWidth = 1
        };

        linePaint = new SKPaint
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

        // The hover node only drives the XAML tooltip - repainting the Skia surface for every
        // pointer move was the main cause of lag on large trees.
        if (e.Property == HoverNodeProperty)
        {
            return;
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
        if (nodePositions.Count == 0)
        {
            return;
        }

        e.Surface.Canvas.Clear(SKColors.Transparent);

        //// Draw levels from the bottom up
        for (var i = levelCount; i >= 0; i--)
        {
            DrawTreeLevel(i, e.Surface.Canvas);
        }
    }

    private float GetNodeX(int n) => (PageWidth + HorizontalMargin) * n;

    private float GetNodeY(int level, int row) => PageHeight + VerticalMargin * level + (PageHeight + VerticalMargin * row);

    /// <summary>
    /// Build a virtual structure of the tree per level
    /// </summary>
    private void BuildIndexTree()
    {
        nodePositions.Clear();
        nodesByLevel.Clear();
        levelMaxX.Clear();
        ordinalByAddress.Clear();
        globalMaxX = 0;
        levelCount = 0;

        if (Nodes.Count == 0)
        {
            return;
        }

        levelCount = Nodes.Max(n => n.Level);

        // Address -> ordinal lookup so DrawLines can resolve parents in O(1) rather than scanning.
        foreach (var node in Nodes)
        {
            ordinalByAddress[node.PageAddress] = node.Ordinal;
        }

        for (var i = levelCount; i >= 0; i--)
        {
            BuildIndexTreeLevel(i, Nodes);
        }

        // Group positions by level and record per-level / global extents once for the paint loop.
        foreach (var treeNode in nodePositions)
        {
            var level = treeNode.Node.Level;

            if (!nodesByLevel.TryGetValue(level, out var list))
            {
                list = [];
                nodesByLevel[level] = list;
            }

            list.Add(treeNode);

            if (!levelMaxX.TryGetValue(level, out var max) || treeNode.X > max)
            {
                levelMaxX[level] = treeNode.X;
            }

            if (treeNode.X > globalMaxX)
            {
                globalMaxX = treeNode.X;
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

            nodePositions.Add(new IndexTreeNode(node, x, y, row, column));

            previousNode = node;
        }
    }

    /// <summary>
    /// Gets the X offset for the start of the level
    /// </summary>
    /// <remarks>
    /// Nodes are created in two phases, the build which is a one-off, and the draw which is performed on every re-render.
    /// 
    /// The centering can change depending on the window size to the X offsets are calculated as part of the draw.
    /// 
    /// The build phase starts each level at X = 0, e.g.
    /// 
    ///     Level 0 |----|
    ///     Level 1 |-----------------|
    ///     Level 2 |------------------------|
    ///     
    /// The offset is calculated for each level based on maximum width less the level width, divided by 2. These offsets center the tree:
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

        var maxWidth = globalMaxX + PageWidth + HorizontalMargin;
        var levelWidth = (levelMaxX.TryGetValue(level, out var max) ? max : 0) + HorizontalMargin;

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

        if (!nodesByLevel.TryGetValue(level, out var levelNodes))
        {
            return;
        }

        var startX = GetLevelStartX(level);
        var nextLevelStartX = GetLevelStartX(level - 1);

        // X position of the next level used to draw lines from the page to the parent
        var renderNextLevelStartX = (nextLevelStartX - xScrollOffset);

        // Read the visible bounds once per level rather than per node - on large levels the loop
        // runs for thousands of nodes and the offscreen ones are culled below.
        var clip = canvas.LocalClipBounds;

        foreach (var node in levelNodes)
        {
            var renderX = startX + node.X - xScrollOffset;

            var renderY = node.Y - yScrollOffset;

            // Only draw the page if it is visible
            if (clip.Contains(renderX, renderY))
            {
                var isHighlighted = HighlightedPageAddresses.Contains(node.Node.PageAddress);
                var isSelected = node.Node.PageAddress == SelectedPageAddress;

                DrawPage(canvas, renderX, renderY, isSelected, isHighlighted);

                if (Zoom > 0.5)
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
            linePaint.Color = highlightedBorderColour;
        }
        else if (isSelected)
        {
            linePaint.Color = selectedLineColour;
        }
        else
        {
            linePaint.Color = lineColour;
        }

        linePaint.StrokeWidth = isSelected || isHighlighted ? 2.5f : 1f;
        linePaint.StrokeJoin = SKStrokeJoin.Round;

        foreach (var parent in node.Parents)
        {
            if (!ordinalByAddress.TryGetValue(parent, out var parentOrdinal))
            {
                continue;
            }

            var parentX = nextLevelStartX + GetNodeX(parentOrdinal);

            var y1Line1 = (float)Math.Floor(y + PageHeight / 2);

            var x2Line1 = (float)Math.Floor(x - HorizontalMargin / 2);

            var y2Line2 = (float)Math.Floor(GetNodeY(node.Level - 1, 0) + PageHeight + (VerticalMargin / 4f) - yScrollOffset);

            var x2Line3 = (float)Math.Floor(parentX + (PageWidth / 2));

            var y2Line4 = (float)Math.Floor(GetNodeY(node.Level - 1, 0) + PageHeight - yScrollOffset);

            // Skip building/drawing the connector if its bounding box is entirely offscreen. This
            // is the main per-frame cost on large levels, so culling it keeps selection repaints
            // (which redraw the whole surface) responsive.
            var lineLeft = Math.Min(x2Line1, x2Line3);
            var lineRight = Math.Max(x, x2Line3);

            if (lineRight < clip.Left || lineLeft > clip.Right || y1Line1 < clip.Top || y2Line4 > clip.Bottom)
            {
                continue;
            }

            linePath.Reset();

            linePath.MoveTo(x, y1Line1);
            linePath.LineTo(x2Line1, y1Line1);
            linePath.LineTo(x2Line1, y2Line2);
            linePath.LineTo(x2Line3, y2Line2);
            linePath.LineTo(x2Line3, y2Line4);

            canvas.DrawPath(linePath, linePaint);
        }
    }

    private void DrawPage(SKCanvas canvas,
                          float x,
                          float y,
                          bool isSelected,
                          bool isHighlighted)
    {
        indexPagePaint.Style = SKPaintStyle.Fill;

        var shadowRect = new SKRect(x + 5, y + 5, x + PageWidth, y + PageHeight);

        canvas.DrawRect(shadowRect, shadowPaint);

        var indexPageRect = new SKRect(x, y, x + PageWidth, y + PageHeight);

        if (isSelected)
        {
            indexPagePaint.Color = selectedBackgroundColour;
        }
        else if (isHighlighted)
        {
            indexPagePaint.Color = highlightedBackgroundColour;
        }
        else
        {
            indexPagePaint.Color = backgroundColour;
        }

        canvas.DrawRect(indexPageRect, indexPagePaint);

        indexPagePaint.Style = SKPaintStyle.Stroke;

        if (isSelected)
        {
            indexPagePaint.Color = selectedBorderColour;
        }
        else if (isHighlighted)
        {
            indexPagePaint.Color = highlightedBorderColour;
        }
        else
        {
            indexPagePaint.Color = borderColour;
        }

        indexPagePaint.StrokeWidth = isSelected || isHighlighted ? 2f : 1f;

        canvas.DrawRect(indexPageRect, indexPagePaint);
    }

    /// <summary>
    /// Draws the lines horizontally indicating the index records
    /// </summary>
    private void DrawPageIndexDetail(SKCanvas canvas, float x, float y)
    {
        var verticalMargin = PageHeight / 6;
        var horizontalMargin = PageWidth * .1f;

        indexPagePaint.Color = SKColors.LightGray;

        indexPagePaint.StrokeWidth = 1;

        for (var i = 1; i < 6; i++)
        {
            canvas.DrawLine(x + horizontalMargin,
                            y + verticalMargin * i,
                            x + PageWidth - horizontalMargin,
                            y + verticalMargin * i,
                            indexPagePaint);
        }
    }

    /// <summary>
    /// Draws the index lines vertically indicating the data record columns
    /// </summary>
    private void DrawPageDataDetail(SKCanvas canvas, float x, float y)
    {
        var verticalMargin = PageHeight * .1f;
        var horizontalMargin = PageWidth / 4;

        indexPagePaint.Color = SKColors.LightGray;

        indexPagePaint.StrokeWidth = 1;

        for (var i = 1; i < 4; i++)
        {
            canvas.DrawLine(x + horizontalMargin * i,
                            y + verticalMargin,
                            x + horizontalMargin * i,
                            y + PageHeight - verticalMargin,
                            indexPagePaint);
        }
    }

    private void UpdateScrollbars()
    {
        if (nodePositions.Count == 0)
        {
            HorizontalScrollBar.Visibility = Visibility.Collapsed;
            HorizontalScrollBar.Maximum = 0;
            HorizontalScrollBar.Value = 0;
            VerticalScrollBar.Visibility = Visibility.Collapsed;
            VerticalScrollBar.Maximum = 0;
            VerticalScrollBar.Value = 0;
            return;
        }

        var maxWidth = globalMaxX + PageWidth + (HorizontalMargin * 2);
        var maxHeight = nodePositions.Max(n => n.Y) + PageHeight + VerticalMargin;

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

        isPointerDown = true;
        isDragging = false;
        dragStart = position;
        dragStartHorizontal = HorizontalScrollBar.Value;
        dragStartVertical = VerticalScrollBar.Value;

        IndexCanvas.CapturePointer(e.Pointer);
    }

    private void IndexCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (isPointerDown && !isDragging)
        {
            // A press with no drag is a click - select the node under the pointer.
            HandleClick(e.GetCurrentPoint(this).Position);
        }

        if (isDragging)
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        }

        isPointerDown = false;
        isDragging = false;

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

        if (isPointerDown)
        {
            var deltaX = position.X - dragStart.X;
            var deltaY = position.Y - dragStart.Y;

            if (!isDragging && (Math.Abs(deltaX) > DragThreshold || Math.Abs(deltaY) > DragThreshold))
            {
                isDragging = true;
                TooltipPopup.IsOpen = false;
                HoverNode = null;
                ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
            }

            if (isDragging)
            {
                Pan(dragStartHorizontal - deltaX, dragStartVertical - deltaY);
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

        var oldZoom = Zoom;
        var factor = delta > 0 ? 1.1f : 1f / 1.1f;
        var newZoom = Math.Clamp(oldZoom * factor, MinZoom, MaxZoom);

        e.Handled = true;

        if (Math.Abs(newZoom - oldZoom) < 0.0001f)
        {
            return;
        }

        // Preserve the scroll ratio so the viewport's centre stays roughly stable across the zoom.
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
        var level = nodePositions.FirstOrDefault(n => y >= n.Y - yScrollOffset
                                                      && y <= n.Y - yScrollOffset + PageHeight)?.Node.Level;

        if (level is null)
        {
            return null;
        }

        var xOffset = GetLevelStartX(level.Value) - xScrollOffset;

        var node = nodePositions.FirstOrDefault(n => x >= xOffset + n.X
                                                     && x <= xOffset + n.X + PageWidth
                                                     && y >= n.Y - yScrollOffset
                                                     && y <= n.Y - yScrollOffset + PageHeight);

        return node?.Node;
    }

    public void Dispose()
    {
        indexPagePaint.Dispose();
        linePaint.Dispose();
        shadowPaint.Dispose();
        linePath.Dispose();

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

public record IndexTreeNode(IndexNode Node, float X, float Y, int Row, int Column);