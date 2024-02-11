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

    public PageAddress? HoverPageAddress
    {
        get => (PageAddress?)GetValue(HoverPageAddressProperty);
        set => SetValue(HoverPageAddressProperty, value);
    }

    public static readonly DependencyProperty HoverPageAddressProperty
        = DependencyProperty.Register(nameof(HoverPageAddress),
            typeof(PageAddress?),
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

    private float[] levelOffsets = Array.Empty<float>();

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
            IsAntialias = true,
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

        var stopwatch = new Stopwatch();

        e.Surface.Canvas.Clear(SKColors.Transparent);

        var levelCount = Nodes.Max(n => n.Level);

        var maxWidth = nodePositions.Max(n => n.X) + (PageWidth) + HorizontalMargin * 2;

        if (IndexCanvas.ActualSize.X > maxWidth)
        {
            maxWidth = IndexCanvas.ActualSize.X;
        }

        Debug.WriteLine($"Calculated dimensions in {stopwatch.Elapsed}");

        //// Draw levels from the bottom up
        for (var i = levelCount; i >= 0; i--)
        {
            DrawTreeLevel(i, maxWidth, e.Surface.Canvas);
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

        levelOffsets = new float[levelCount + 1];

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

    private void DrawTreeLevel(int level,
                               float maxWidth,
                               SKCanvas canvas)
    {
        var stopwatch = new Stopwatch();

        var xScrollOffset = (float)HorizontalScrollBar.Value;
        var yScrollOffset = (float)VerticalScrollBar.Value;

        var levelWidth = nodePositions.Where(n => n.Node.Level == level).Max(n => n.X) + PageWidth;

        var nextLevelWidth = level > 0 ? nodePositions.Where(n => n.Node.Level == level - 1)
                                                      .Max(n => n.X) + PageWidth : 0;

        var startX = (maxWidth - levelWidth) / 2;
        var nextLevelStartX = (maxWidth - nextLevelWidth) / 2;

        levelOffsets[level] = startX;

        Debug.WriteLine($"Calculated level dimensions in {stopwatch.Elapsed}");

        stopwatch.Restart();

        var levelNodes = nodePositions.Where(n => n.Node.Level == level).ToList();

        Debug.WriteLine($"Got nodes in {stopwatch.Elapsed}");

        stopwatch.Restart();

        foreach (var node in levelNodes)
        {
            stopwatch.Restart();

            var renderX = (startX + node.X - xScrollOffset);

            var renderY = (node.Y - yScrollOffset);

            // Only draw the page if it is visible
            if (canvas.LocalClipBounds.Contains(renderX, renderY))
            {
                var isHighlighted = HighlightedPageAddresses.Contains(node.Node.PageAddress);
                var isSelected = node.Node.PageAddress == SelectedPageAddress;

                DrawPage(canvas, renderX, renderY, indexPagePaint, isSelected, isHighlighted);

                if (node.Node.PageType == PageType.Index)
                {
                    DrawIndex(canvas, renderX, renderY, indexPagePaint);
                }
                else
                {
                    DrawData(canvas, renderX, renderY, indexPagePaint);
                }
            }

            var renderNextLevelStartX = (nextLevelStartX - xScrollOffset);

            DrawLines(canvas, node.Node, renderX, renderY, renderNextLevelStartX, yScrollOffset, false, false);

        }

        Debug.WriteLine($"Node/Line Draw in {stopwatch.Elapsed}");

        stopwatch.Restart();

        // Draw highlighted page lines on top of existing lines
        if (HighlightedPageAddresses.Any())
        {
            foreach (var pageAddress in HighlightedPageAddresses)
            {
                var node = nodePositions.FirstOrDefault(n => n.Node.PageAddress == pageAddress
                                                              && n.Node.Level == level);

                if (node != null)
                {
                    var renderX = (startX + node.X - xScrollOffset);

                    var renderY = (node.Y - yScrollOffset);

                    var renderNextLevelStartX = (nextLevelStartX - xScrollOffset);

                    DrawLines(canvas, node.Node, renderX, renderY, renderNextLevelStartX, yScrollOffset, false, true);
                }
            }
        }

        Debug.WriteLine($"Page highlight in {stopwatch.Elapsed}");

        stopwatch.Restart();

        // Draw selected page lines on top of existing lines
        if (SelectedPageAddress != null)
        {
            stopwatch.Restart();

            var node = nodePositions.FirstOrDefault(n => n.Node.PageAddress == SelectedPageAddress
                                                         && n.Node.Level == level);

            if (node != null)
            {
                var renderX = (startX + node.X - xScrollOffset);

                var renderY = (node.Y - yScrollOffset);

                var renderNextLevelStartX = (nextLevelStartX - xScrollOffset);

                DrawLines(canvas, node.Node, renderX, renderY, renderNextLevelStartX, yScrollOffset, true, false);
            }
        }

        Debug.WriteLine($"Selected page in {stopwatch.Elapsed}");
    }

    /// <summary>
    /// Draws line(s) to parent node(s)
    /// </summary>
    private void DrawLines(SKCanvas canvas,
                           IndexNode node,
                           float x, float y,
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

        linePaint.Color = isSelected ? selectedLineColour : lineColour;

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

        linePaint.StrokeWidth = isSelected || isHighlighted ? 3 : 1;
        linePaint.StrokeJoin = SKStrokeJoin.Round;

        foreach (var parent in node.Parents)
        {
            var parentOrdinal = Nodes.First(n => n.PageAddress == parent).Ordinal;

            var parentX = nextLevelStartX + GetNodeX(parentOrdinal);

            var y1Line1 = y + PageHeight / 2;

            var x2Line1 = x - HorizontalMargin / 2;

            var y2Line2 = GetNodeY(node.Level - 1, 0) + PageHeight + (VerticalMargin / 4f) - yScrollOffset;

            var x2Line3 = parentX + (PageWidth / 2);

            var y2Line4 = GetNodeY(node.Level - 1, 0) + PageHeight - yScrollOffset;

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
                          SKPaint paint,
                          bool isSelected,
                          bool isHighlighted)
    {
        var indexPageRect = new SKRect(x, y, x + PageWidth, y + PageHeight);
        var shadowRect = new SKRect(x + 5, y + 5, x + PageWidth, y + PageHeight);

        paint.Style = SKPaintStyle.Fill;
        paint.Color = isHighlighted ? highlightedBackgroundColour : backgroundColour;

        canvas.DrawRect(shadowRect, shadowPaint);
        canvas.DrawRect(indexPageRect, paint);

        paint.Style = SKPaintStyle.Stroke;
        paint.Color = isSelected ? selectedBorderColour : borderColour;

        if (isHighlighted)
        {
            paint.Color = highlightedBorderColour;
        }
        else if (isSelected)
        {
            paint.Color = selectedBorderColour;
        }
        else
        {
            paint.Color = borderColour;
        }

        paint.StrokeWidth = isSelected || isHighlighted ? 2 : 1;

        canvas.DrawRect(indexPageRect, paint);
    }

    private void DrawIndex(SKCanvas canvas,
                           float x,
                           float y,
                           SKPaint paint)
    {
        var verticalMargin = PageHeight / 6;
        var horizontalMargin = PageWidth * .1f;

        paint.Color = SKColors.LightGray;

        paint.StrokeWidth = 1;

        for (var i = 1; i < 6; i++)
        {
            canvas.DrawLine(x + horizontalMargin,
                            y + verticalMargin * i,
                            x + PageWidth - horizontalMargin,
                            y + verticalMargin * i,
                            paint);
        }
    }

    private void DrawData(SKCanvas canvas,
                          float x,
                          float y,
                          SKPaint paint)
    {
        var verticalMargin = PageHeight * .1f;
        var horizontalMargin = PageWidth / 4;

        paint.Color = SKColors.LightGray;

        paint.StrokeWidth = 1;

        for (var i = 1; i < 4; i++)
        {
            canvas.DrawLine(x + horizontalMargin * i,
                            y + verticalMargin,
                            x + horizontalMargin * i,
                            y + PageHeight - verticalMargin,
                            paint);
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

    private void IndexCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(this).Position;
        var node = GetIndexNodeAtPosition(position.X, position.Y);

        if (node is not null)
        {
            HoverPageAddress = node.PageAddress;

            TooltipPopup.HorizontalOffset = position.X + 10;
            TooltipPopup.VerticalOffset = position.Y + 10;
            TooltipPopup.IsOpen = true;
        }
        else
        {
            TooltipPopup.IsOpen = false;
            HoverPageAddress = null;
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

        var xOffset = levelOffsets[level.Value] - xScrollOffset;
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