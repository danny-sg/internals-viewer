using InternalsViewer.Internals.Engine.Indexes;
using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using InternalsViewer.Internals.Engine.Address;
using Microsoft.UI.Xaml.Controls.Primitives;
using InternalsViewer.UI.App.Controls.Allocation;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Controls.Index;

public sealed partial class IndexControl
{
    public event EventHandler<PageNavigationEventArgs>? PageClicked;

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

    private float IndexPageWidth => 20 * Zoom;
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

    private readonly List<(IndexNode Node, float X, float Y, int Row, int Column)> nodePositions = new();

    private float[] levelOffsets = Array.Empty<float>();

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (IndexControl)d;

        if (e.Property == ZoomProperty || e.Property == NodesProperty)
        {
            control.BuildIndexTree();
        }

        control.IndexCanvas.Invalidate();
    }

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

    private void IndexCanvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        if (!nodePositions.Any())
        {
            return;
        }

        e.Surface.Canvas.Clear(SKColors.Transparent);

        var levelCount = Nodes.Max(n => n.Level);

        var maxWidth = nodePositions.Max(n => n.X) + (IndexPageWidth) + HorizontalMargin * 2;

        if (IndexCanvas.ActualSize.X > maxWidth)
        {
            maxWidth = IndexCanvas.ActualSize.X;
        }

        //// Draw levels from the bottom up
        for (var i = levelCount; i >= 0; i--)
        {
            DrawTreeLevel(i, maxWidth, e.Surface.Canvas);
        }
    }

    private float GetNodeX(int n) => (IndexPageWidth + HorizontalMargin) * n;

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

            nodePositions.Add((Node: node, X: x, Y: y, Row: row, Column: column));

            previousNode = node;
        }
    }

    private void DrawTreeLevel(int level,
                               float maxWidth,
                               SKCanvas canvas)
    {
        var xScrollOffset = (float)HorizontalScrollBar.Value;
        var yScrollOffset = (float)VerticalScrollBar.Value;

        var levelWidth = nodePositions.Where(n => n.Node.Level == level).Max(n => n.X) + IndexPageWidth;

        var nextLevelWidth = level > 0 ? nodePositions.Where(n => n.Node.Level == level - 1)
                                                      .Max(n => n.X) + IndexPageWidth : 0;

        var startX = (maxWidth - levelWidth) / 2;
        var nextLevelStartX = (maxWidth - nextLevelWidth) / 2;

        levelOffsets[level] = startX;

        var levelNodes = nodePositions.Where(n => n.Node.Level == level).ToList();

        foreach (var node in levelNodes)
        {
            var renderX = (startX + node.X - xScrollOffset);

            var renderY = (node.Y - yScrollOffset);

            // Only draw the page if it is visible
            if (canvas.LocalClipBounds.Contains(renderX, renderY))
            {
                DrawIndexPage(canvas, renderX, renderY, indexPagePaint);
            }

            var renderNextLevelStartX = (nextLevelStartX - xScrollOffset);

            DrawLines(canvas, node.Node, renderX, renderY, renderNextLevelStartX, yScrollOffset, false);
        }

        // Draw selected page lines on top of existing lines
        if (SelectedPageAddress != null)
        {
            var node = nodePositions.FirstOrDefault(n => n.Node.PageAddress == SelectedPageAddress
                                                         && n.Node.Level == level);

            if (node.Node != null)
            {
                var renderX = (startX + node.X - xScrollOffset);

                var renderY = (node.Y - yScrollOffset);

                var renderNextLevelStartX = (nextLevelStartX - xScrollOffset);

                DrawLines(canvas, node.Node, renderX, renderY, renderNextLevelStartX, yScrollOffset, true);
            }
        }
    }

    /// <summary>
    /// Draws line(s) to parent node(s)
    /// </summary>
    private void DrawLines(SKCanvas canvas,
                           IndexNode node,
                           float x, float y,
                           float nextLevelStartX,
                           float yScrollOffset,
                           bool isSelected)
    {
        //            X         Parent Node 
        //            | Line 4
        //      ------  Line 3  
        //      |       Line 2
        //      --X     Line 1  Node    

        linePaint.Color = isSelected ? SKColors.Green : SKColors.Gray;
        linePaint.StrokeWidth = isSelected ? 4 : 1;
        linePaint.StrokeJoin = SKStrokeJoin.Round;

        foreach (var parent in node.Parents)
        {
            var parentOrdinal = Nodes.First(n => n.PageAddress == parent).Ordinal;

            var parentX = nextLevelStartX + GetNodeX(parentOrdinal);

            var y1Line1 = y + PageHeight / 2;
            var x2Line1 = x - HorizontalMargin / 2;

            var y2Line2 = GetNodeY(node.Level - 1, 0) + PageHeight + (VerticalMargin / 4f) - yScrollOffset;

            var x2Line3 = parentX + (IndexPageWidth / 2);

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

    private void DrawIndexPage(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        var indexPageRect = new SKRect(x, y, x + IndexPageWidth, y + PageHeight);
        var shadowRect = new SKRect(x + 5, y + 5, x + IndexPageWidth, y + PageHeight);

        paint.Style = SKPaintStyle.Fill;
        paint.Color = SKColors.White;

        canvas.DrawRect(shadowRect, shadowPaint);
        canvas.DrawRect(indexPageRect, paint);

        paint.Style = SKPaintStyle.Stroke;
        paint.Color = SKColors.Gray;
        paint.StrokeWidth = 1;

        canvas.DrawRect(indexPageRect, paint);

        var verticalMargin = PageHeight / 6;
        var horizontalMargin = IndexPageWidth * .1f;

        paint.Color = SKColors.LightGray;

        for (var i = 1; i < 6; i++)
        {
            canvas.DrawLine(x + horizontalMargin,
                            y + verticalMargin * i,
                            x + IndexPageWidth - horizontalMargin,
                            y + verticalMargin * i
                            , paint);
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
            SetScrollbars(nodePositions.Max(n => n.X) + IndexPageWidth + (HorizontalMargin * 2));
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
        var level = nodePositions.FirstOrDefault(n => y >= n.Y && y <= n.Y + PageHeight).Node?.Level;

        if (level is null)
        {
            return null;
        }

        var xScrollOffset = (float)HorizontalScrollBar.Value;
        var yScrollOffset = (float)VerticalScrollBar.Value;

        var xOffset = levelOffsets[level.Value] - xScrollOffset;
        var yOffset = yScrollOffset;

        var node = nodePositions.FirstOrDefault(n => x >= xOffset + n.X
                                                     && x <= xOffset + n.X + IndexPageWidth
                                                     && y >= yOffset + n.Y
                                                     && y <= yOffset + n.Y + PageHeight);

        return node.Node;
    }
}
public class ViewIndexEventArgs(long allocationUnitId) : EventArgs
{
    public long AllocationUnitId { get; } = allocationUnitId;
}