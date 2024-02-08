using InternalsViewer.Internals.Engine.Indexes;
using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System.Diagnostics;
using InternalsViewer.Internals.Engine.Address;

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
            StrokeWidth = 2f
        };
    }

    private void IndexCanvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        nodePositions.Clear();

        e.Surface.Canvas.Clear(SKColors.Transparent);

        var levelCount = Nodes.Max(n => n.Level);

        var levelNodeCounts = Nodes.GroupBy(n => n.Level).ToDictionary(g => g.Key, g => g.Count());

        Debug.Print($"Level Count: {levelCount}");

        var totalWidth = (levelNodeCounts[levelCount] * (NodeWidth + HorizontalMargin)) + (HorizontalMargin * 2);

        IndexCanvas.Width = totalWidth;
        // IndexCanvas.Height = totalHeight;

        for (var i = levelCount; i >= 0; i--)
        {
            DrawLevel(i, e.Surface.Canvas, Nodes, totalWidth, levelNodeCounts);
        }
    }

    private void DrawLevel(int level,
                           SKCanvas canvas,
                           List<IndexNode> nodes,
                           float totalWidth,
                           Dictionary<int, int> levelNodeCounts)
    {
        Debug.Print($"Level 1: {level}, Count: {levelNodeCounts[level]}");

        var levelWidth = levelNodeCounts[level] * (NodeWidth + HorizontalMargin);

        float nextLevelStartX = 0;

        if (level > 0)
        {
            var nextLevelWidth = levelNodeCounts[level - 1] * (NodeWidth + HorizontalMargin);

            nextLevelStartX = (totalWidth - nextLevelWidth) / 2;
        }

        var startX = (totalWidth - levelWidth) / 2;

        var y = GetNodeY(level);

        var levelNodes = nodes.Where(n => n.Level == level).ToList();

        var n = 0;

        foreach (var node in levelNodes)
        {
            var x = startX + GetNodeX(n);

            nodePositions.Add((X: x, Y: y, node.PageAddress));

            if (selectedNodes.Any(h => h == node.PageAddress))
            {
                DrawNode(canvas, x, y, hoverPagePaint, node.PageAddress.ToString());
            }
            else if (hoverNodes.Any(h => h == node.PageAddress))
            {
                DrawNode(canvas, x, y, hoverPagePaint, node.PageAddress.ToString());
            }
            else
            {
                DrawNode(canvas, x, y, pagePaint, node.PageAddress.ToString());
            }

            DrawLines(canvas, node, x, y, nextLevelStartX);

            n++;
        }
    }

    private void DrawLines(SKCanvas canvas, IndexNode node, float x, float y, float nextLevelStartX)
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

            var parentX = nextLevelStartX + GetNodeX(parentOrdinal - 1);

            var y1Line1 = y + PageHeight / 2;
            var x2Line1 = x - HorizontalMargin / 2;

            var y2Line2 = y - PageHeight / 2;

            var x2Line3 = parentX + (NodeWidth / 2);

            var y2Line4 = GetNodeY(node.Level - 1) + PageHeight;

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

    private float GetNodeY(int level) => PageHeight + VerticalMargin * level;

    private void DrawNode(SKCanvas canvas, float x, float y, SKPaint paint, string label)
    {
        var rect = new SKRect(x, y, x + NodeWidth, y + PageHeight);

        canvas.DrawRect(rect, paint);

        if (!string.IsNullOrEmpty(label))
        {
            var textPaint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = 10 * Zoom
            };

            canvas.DrawText(label, x - 2, y + PageHeight + 12, textPaint);
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
}

public class ViewIndexEventArgs(long allocationUnitId) : EventArgs
{
    public long AllocationUnitId { get; } = allocationUnitId;
}