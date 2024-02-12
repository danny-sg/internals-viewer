using InternalsViewer.Internals.Engine.Indexes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Windows.System;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using InternalsViewer.Internals.Engine.Address;
using Microsoft.UI.Xaml.Controls.Primitives;
using InternalsViewer.UI.App.Controls.Allocation;
using Microsoft.UI.Xaml.Input;
using static System.Windows.Forms.AxHost;
using Windows.UI.Core;
using InternalsViewer.Internals.Engine.Pages.Enums;
using Microsoft.UI.Input;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace InternalsViewer.UI.App.Controls.Index;

public sealed partial class IndexControl
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

    private readonly List<IndexTreeNode> nodePositions = new();

    public IndexControl()
    {
        InitializeComponent();

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

        if (e.Property == ZoomProperty || e.Property == NodesProperty)
        {
            control.BuildIndexTree();
        }

        control.IndexCanvas.Invalidate();
    }

    private void IndexCanvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        if (!nodePositions.Any())
        {
            return;
        }

        e.Surface.Canvas.Clear(SKColors.Transparent);

        var levelCount = Nodes.Max(n => n.Level);

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

        if (!Nodes.Any())
        {
            return;
        }

        var levelCount = Nodes.Max(n => n.Level);

        for (var i = levelCount; i >= 0; i--)
        {
            BuildIndexTreeLevel(i, Nodes);
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

        var maxWidth = nodePositions.Max(n => n.X) + PageWidth + HorizontalMargin;
        var levelWidth = nodePositions.Where(n => n.Node.Level == level).Max(n => n.X) + HorizontalMargin;

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

        var startX = GetLevelStartX(level);
        var nextLevelStartX = GetLevelStartX(level - 1);

        // Nodes for this level
        var levelNodes = nodePositions.Where(n => n.Node.Level == level).ToList();

        // X position of the next level used to draw lines from the page to the parent
        var renderNextLevelStartX = (nextLevelStartX - xScrollOffset);

        foreach (var node in levelNodes)
        {
            var renderX = startX + node.X - xScrollOffset;

            var renderY = node.Y - yScrollOffset;

            // Only draw the page if it is visible
            if (canvas.LocalClipBounds.Contains(renderX, renderY))
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

            DrawLines(canvas, node.Node, renderX, renderY, renderNextLevelStartX, yScrollOffset, false, false);
        }
    }

    /// <summary>
    /// Draws line(s) to parent node(s)
    /// </summary>
    private void DrawLines(SKCanvas canvas,
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
            var parentOrdinal = Nodes.First(n => n.PageAddress == parent).Ordinal;

            var parentX = nextLevelStartX + GetNodeX(parentOrdinal);

            var y1Line1 = (float)Math.Floor(y + PageHeight / 2);

            var x2Line1 = (float)Math.Floor(x - HorizontalMargin / 2);

            var y2Line2 = (float)Math.Floor(GetNodeY(node.Level - 1, 0) + PageHeight + (VerticalMargin / 4f) - yScrollOffset);

            var x2Line3 = (float)Math.Floor(parentX + (PageWidth / 2));

            var y2Line4 = (float)Math.Floor(GetNodeY(node.Level - 1, 0) + PageHeight - yScrollOffset);

            var path = new SKPath();

            path.MoveTo(x, y1Line1);
            path.LineTo(x2Line1, y1Line1);
            path.LineTo(x2Line1, y2Line2);
            path.LineTo(x2Line3, y2Line2);
            path.LineTo(x2Line3, y2Line4);

            canvas.DrawPath(path, linePaint);
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

    private void SetScrollbars(float maxWidth)
    {
        if (maxWidth < IndexCanvas.ActualSize.X)
        {
            HorizontalScrollBar.Visibility = Visibility.Collapsed;
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
        }
    }

    private void ScrollBar_OnScroll(object sender, ScrollEventArgs e)
    {
        IndexCanvas.Invalidate();
    }

    private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (nodePositions.Any())
        {
            SetScrollbars(nodePositions.Max(n => n.X) + PageWidth + (HorizontalMargin * 2));
        }
    }

    private void IndexCanvas_OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        TooltipPopup.IsOpen = false;
    }

    private void IndexCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(this).Position;

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
    /// Check if the pointer is over a node and display the tooltip
    /// </summary>
    private void IndexCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(this).Position;
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

    private IndexNode? GetIndexNodeAtPosition(double x, double y)
    {
        // Find the level first as the level offsets are used to center the tree
        var level = nodePositions.FirstOrDefault(n => y >= n.Y && y <= n.Y + PageHeight)?.Node.Level;

        if (level is null)
        {
            return null;
        }

        var xScrollOffset = (float)HorizontalScrollBar.Value;
        var yScrollOffset = (float)VerticalScrollBar.Value;

        var xOffset = GetLevelStartX(level.Value) - xScrollOffset;
        var yOffset = yScrollOffset;

        var node = nodePositions.FirstOrDefault(n => x >= xOffset + n.X
                                                     && x <= xOffset + n.X + PageWidth
                                                     && y >= yOffset + n.Y
                                                     && y <= yOffset + n.Y + PageHeight);

        return node?.Node;
    }
}

public class ViewIndexEventArgs(long allocationUnitId) : EventArgs
{
    public long AllocationUnitId { get; } = allocationUnitId;
}

public record IndexTreeNode(IndexNode Node, float X, float Y, int Row, int Column);