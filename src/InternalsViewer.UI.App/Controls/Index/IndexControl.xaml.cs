using InternalsViewer.Internals.Engine.Indexes;
using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using InternalsViewer.Internals.Engine.Address;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace InternalsViewer.UI.App.Controls.Index;

public sealed partial class IndexControl
{
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

    private readonly List<(IndexNode Node, float X, float Y, int Row, int Column)> nodePositions = new();

    private float[] levelOffsets = Array.Empty<float>();

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (IndexControl)d;

        control.BuildIndexTree();

        control.IndexCanvas.Invalidate();
    }

    public IndexControl()
    {
        InitializeComponent();

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

        var nextLevelWidth = level > 0 ? nodePositions.Where(n => n.Node.Level == level - 1).Max(n => n.X) + IndexPageWidth : 0;

        var startX = (maxWidth - levelWidth) / 2;
        var nextLevelStartX = (maxWidth - nextLevelWidth) / 2;

        levelOffsets[level] = startX;

        var levelNodes = nodePositions.Where(n => n.Node.Level == level).ToList();

        foreach (var node in levelNodes)
        {
            var renderX = (startX + node.X - xScrollOffset);

            var renderY = (node.Y - yScrollOffset);

            if (canvas.LocalClipBounds.Contains(renderX, renderY))
            {
                DrawIndexPage(canvas, renderX, renderY, indexPagePaint);

                var renderNextLevelStartX = (nextLevelStartX - xScrollOffset);

                DrawLines(canvas, node.Node, renderX, renderY, renderNextLevelStartX, yScrollOffset);
            }
        }
    }

    private void DrawLines(SKCanvas canvas,
                           IndexNode node, float x, float y, float nextLevelStartX, float yScrollOffset)
    {
        // Draws line(s) to parent node(s)
        //
        //            X         Parent Node 
        //            | Line 4
        //      ------  Line 3  
        //      |       Line 2
        //      --X     Line 1  Node    

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
        var rect = new SKRect(x, y, x + IndexPageWidth, y + PageHeight);

        paint.Style = SKPaintStyle.Fill;
        paint.Color = SKColors.White;

        canvas.DrawRect(rect, paint);

        paint.Style = SKPaintStyle.Stroke;
        paint.Color = SKColors.Gray;
        paint.StrokeWidth = 1;

        canvas.DrawRect(rect, paint);

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
            HorizontalScrollBar.Visibility = Visibility.Visible;
            HorizontalScrollBar.Maximum = maxWidth;
            HorizontalScrollBar.SmallChange = 1;
            HorizontalScrollBar.LargeChange = IndexPageWidth + HorizontalMargin;
        }
    }

    private void IndexCanvas_PointerAction(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(this).Position;

        var isClick = e.Pointer.IsInContact;

        var level = nodePositions.FirstOrDefault(n => position.Y >= n.Y && position.Y <= n.Y + PageHeight).Node?.Level;

        if(level is null)
        {
            return;
        }

        var xOffset = levelOffsets[level.Value];
                                                 
        var node = nodePositions.FirstOrDefault(n => position.X >= xOffset + n.X
                                                     && position.X <= xOffset + n.X + IndexPageWidth
                                                     && position.Y >= n.Y
                                                     && position.Y <= n.Y + PageHeight);

        if (node.Node is not null)
        {
            TooltipPopup.IsOpen = true;
            HoverPageAddress = node.Node.PageAddress;

            TooltipPopup.HorizontalOffset = position.X + 10;
            TooltipPopup.VerticalOffset = position.Y + 10;
        }
        else
        {
            TooltipPopup.IsOpen = false;
            HoverPageAddress = null;
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
            SetScrollbars(nodePositions.Max(n => n.X) + IndexPageWidth);
        }
    }
}

public class ViewIndexEventArgs(long allocationUnitId) : EventArgs
{
    public long AllocationUnitId { get; } = allocationUnitId;
}