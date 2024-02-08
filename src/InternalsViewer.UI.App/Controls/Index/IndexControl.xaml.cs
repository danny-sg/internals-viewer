using InternalsViewer.Internals.Engine.Indexes;
using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System.Diagnostics;
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

    private float NodeWidth => 20 * Zoom;
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

    private readonly SKPaint pagePaint;
    private readonly SKPaint hoverPagePaint;
    private readonly SKPaint linePaint;

    private float leafWidth = 0;

    private readonly List<(float X, float Y, PageAddress PageAddress)> nodePositions = new();

    private List<PageAddress> hoverNodes = new();
    private List<PageAddress> selectedNodes = new();

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (IndexControl)d;

        control.IndexCanvas.Invalidate();
    }

    public IndexControl()
    {
        InitializeComponent();

        pagePaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.Gray,
            IsAntialias = true,
            StrokeWidth = 1
        };

        hoverPagePaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.Green,
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
        nodePositions.Clear();

        if(!Nodes.Any())
        {
            return;
        }

        e.Surface.Canvas.Clear(SKColors.Transparent);

        var levelCount = Nodes.Max(n => n.Level);

        var levelNodeCounts = Nodes.GroupBy(n => n.Level).ToDictionary(g => g.Key, g => g.Count());

        Debug.Print($"Level Count: {levelCount}");

        // Draw levels from the bottom up
        for (var i = levelCount; i >= 0; i--)
        {
            var width = DrawLevel(i, i == levelCount, e.Surface.Canvas, Nodes, levelNodeCounts);

            if (i == levelCount)
            {
                leafWidth = width;
            }
        }

        SetScrollbars(leafWidth);
    }

    private void SetScrollbars(float maxWidth)
    {
        if(maxWidth < IndexCanvas.ActualSize.X)
        {
            HorizontalScrollBar.Visibility = Visibility.Collapsed;
            HorizontalScrollBar.Value = 0;
        }
        else
        {
            HorizontalScrollBar.Visibility = Visibility.Visible;
            HorizontalScrollBar.Maximum = maxWidth;
            HorizontalScrollBar.SmallChange = 1;
            HorizontalScrollBar.LargeChange = NodeWidth + HorizontalMargin;
        }
    }

    /// <summary>
    /// Draws a level of nodes
    /// </summary>
    /// <remarks>
    /// Levels are drawn from the bottom up as the layout is based on the widest part of the tree with the most nodes.
    /// 
    /// The indexes come out flat and wide, so if necessary the last level is organised into rows and columns.
    /// 
    /// Nodes are placed vertically per VerticalNodeCount but if the parent is different a new column is started.
    /// </remarks>
    private float DrawLevel(int level,
                            bool isFirstLevel,
                            SKCanvas canvas,
                            List<IndexNode> nodes,
                            Dictionary<int, int> levelNodeCounts)
    {
        var xScrollOffset = (float)HorizontalScrollBar.Value;
        var yScrollOffset = (float)VerticalScrollBar.Value;

        var verticalNodeCount = isFirstLevel ? 10 : 1;

        Debug.Print($"Level 1: {level}, Count: {levelNodeCounts[level]}");

        var levelWidth = levelNodeCounts[level] * (NodeWidth + HorizontalMargin);

        float nextLevelStartX = 0;

        var startX = isFirstLevel ? HorizontalMargin : (leafWidth - levelWidth) / 2;

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

            var y = GetNodeY(level, row - 1) - yScrollOffset;

            var x = startX + GetNodeX(column - 1) - xScrollOffset;

            nodePositions.Add((X: x, Y: y, node.PageAddress));

            if(canvas.LocalClipBounds.Contains(x, y))
            {
                DrawIndexPage(canvas, x, y, pagePaint);
            }

            previousNode = node;
        }

        if (level > 0)
        {
            var nextLevelWidth = levelNodeCounts[level - 1] * (NodeWidth + HorizontalMargin);

            nextLevelStartX = (leafWidth - nextLevelWidth) / 2;
        }

        // Second pass for the lines
        previousNode = null;
        column = 1;
        row = 1;

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

            var y = GetNodeY(level, row - 1) - yScrollOffset;
            var x = startX + GetNodeX(column - 1) - xScrollOffset;

            var renderNextLevelStartX = nextLevelStartX - xScrollOffset;

            DrawLines(canvas, node, row, x, y, renderNextLevelStartX, xScrollOffset, yScrollOffset);

            previousNode = node;
        }

        return startX + GetNodeX(column);
    }

    private void DrawLines(SKCanvas canvas, IndexNode node, int row, float x, float y, float nextLevelStartX, float xScrollOffset, float yScrollOffset)
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

            var x2Line3 = parentX + (NodeWidth / 2);

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

    private float GetNodeX(int n) => (NodeWidth + HorizontalMargin) * n;

    private float GetNodeY(int level, int row) => (PageHeight + VerticalMargin * level) + (PageHeight + VerticalMargin * row);

    private void DrawIndexPage(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        var rect = new SKRect(x, y, x + NodeWidth, y + PageHeight);

        paint.Style = SKPaintStyle.Fill;
        paint.Color = SKColors.White;

        canvas.DrawRect(rect, paint);

        paint.Style = SKPaintStyle.Stroke;
        paint.Color = SKColors.Gray;
        paint.StrokeWidth = 1;

        canvas.DrawRect(rect, paint);

        var verticalMargin = PageHeight / 6;
        var horizontalMargin = NodeWidth * .1f;

        paint.Color = SKColors.LightGray;

        for (var i = 1; i < 6; i++)
        {
            canvas.DrawLine(x + horizontalMargin,
                            y + verticalMargin * i,
                            x + NodeWidth - horizontalMargin,
                            y + verticalMargin * i
                            , paint);
        }
    }

    private void IndexCanvas_PointerAction(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(this).Position;

        var isClick = e.Pointer.IsInContact;

        var node = nodePositions.FirstOrDefault(n => position.X >= n.X
                                                     && position.X <= n.X + NodeWidth
                                                     && position.Y >= n.Y
                                                     && position.Y <= n.Y + PageHeight);

        if (node.PageAddress == PageAddress.Empty && hoverNodes.Any())
        {
            hoverNodes.Clear();

            if (isClick)
            {
                selectedNodes.Clear();
            }

            IndexCanvas.Invalidate();

            return;
        }

        if (isClick)
        {
            selectedNodes.Clear();
            selectedNodes.Add(node.PageAddress);
        }
        else
        {
            hoverNodes.Add(node.PageAddress);
        }

        var parents = Nodes.Where(n => n.PageAddress == node.PageAddress).SelectMany(n => n.Parents).ToList();

        while (parents.Any())
        {
            if (isClick)
            {
                selectedNodes.AddRange(parents);
            }
            else
            {
                hoverNodes.AddRange(parents);
            }

            parents = Nodes.Where(n => parents.Contains(n.PageAddress)).SelectMany(n => n.Parents).ToList();
        }

        IndexCanvas.Invalidate();
    }

    private void VerticalScrollBar_OnScroll(object sender, ScrollEventArgs e)
    {
        throw new NotImplementedException();
    }

    private void ScrollBar_OnScroll(object sender, ScrollEventArgs e)
    {
        IndexCanvas.Invalidate();
    }
}

public class ViewIndexEventArgs(long allocationUnitId) : EventArgs
{
    public long AllocationUnitId { get; } = allocationUnitId;
}