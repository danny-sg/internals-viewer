using InternalsViewer.Internals.Engine.Indexes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using SkiaSharp;
using System.Xml.Linq;
using InternalsViewer.Internals.Engine.Address;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI;
using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;

namespace InternalsViewer.UI.App.Controls.Index;

public sealed partial class IndexControl
{
    private const float MinimumZoom = 0.2f;
    private const float MaximumZoom = 4f;

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

    private float PageWidth => 40 * Zoom;
    private float PageHeight => 60 * Zoom;
    private float HorizontalMargin => 60 * Zoom;
    private float VerticalMargin => 140 * Zoom;

    public IndexNode RootNode
    {
        get => (IndexNode)GetValue(RootNodeProperty);
        set => SetValue(RootNodeProperty, value);
    }

    public static readonly DependencyProperty RootNodeProperty
        = DependencyProperty.Register(nameof(RootNode),
                                      typeof(IndexNode),
                                      typeof(IndexControl),
                                      new PropertyMetadata(new IndexNode(PageAddress.Empty), OnPropertyChanged));

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (IndexControl)d;

        control.AddIndex(control.RootNode, control.IndexCanvas);
    }

    private void AddIndex(IndexNode node, Canvas canvas)
    {
        canvas.Children.Clear();

        var nodeLevels = CountNodesPerLevel(node);

        canvas.Width = nodeLevels.Max(kvp => kvp.Value) * (PageWidth + HorizontalMargin + 1);
        canvas.Height = nodeLevels.Max(kvp => kvp.Key) * (PageHeight + VerticalMargin + 1);

        AddPage(canvas, node, (canvas.Width / 2) - (PageWidth / 2), 50, nodeLevels);
    }

    private void Index_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);

        var isControlPressed = state.HasFlag(CoreVirtualKeyStates.Down);

        if (isControlPressed)
        {
            var newZoom = Zoom + e.GetCurrentPoint(this).Properties.MouseWheelDelta / 1000F;

            if (newZoom is >= MinimumZoom and <= MaximumZoom)
            {
                Zoom = newZoom;
            }
        }
        else
        {
            //IndexScrollViewer.Val -= e.GetCurrentPoint(this).Properties.MouseWheelDelta;
        }
    }


    private static Dictionary<int, int> CountNodesPerLevel(IndexNode node)
    {
        var visited = new List<PageAddress>();

        var result = new Dictionary<int, int>();

        CountLevel(node);

        return result;

        void CountLevel(IndexNode indexNode)
        {
            if (!visited.Contains(indexNode.PageAddress))
            {
                visited.Add(indexNode.PageAddress);

                if (result.ContainsKey(indexNode.Level))
                {
                    result[indexNode.Level]++;
                }
                else
                {
                    result.Add(indexNode.Level, 1);
                }
            }

            foreach (var child in indexNode.Children)
            {
                CountLevel(child);
            }
        }
    }

    private void AddPage(Canvas canvas, IndexNode sourceNode, double x, float y, Dictionary<int, int> nodeLevels)
    {
        // Draw the box for the current node
        var rect = new Rectangle
        {
            Width = PageWidth,
            Height = PageHeight,
            Fill = new SolidColorBrush(Colors.Gray)
        };

        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);

        canvas.Children.Add(rect);

        if (!sourceNode.Children.Any())
        {
            return;
        }

        var nextY = y + PageHeight + VerticalMargin;

        var siblingCount = nodeLevels[sourceNode.Level + 1];

        var totalWidth = siblingCount * PageWidth + (siblingCount - 1) * HorizontalMargin;

        var startX = (canvas.Width / 2) - (totalWidth / 2);

        foreach (var node in sourceNode.Children)
        {
            var line = new Line();

            line.Stroke = new SolidColorBrush(Colors.IndianRed);
            line.StrokeThickness = 1;
            line.StrokeDashArray = new DoubleCollection { 2 };

            line.X1 = x + PageWidth / 2;
            line.Y1 = y + PageHeight;
            line.X2 = startX + PageWidth / 2;
            line.Y2 = nextY;

            canvas.Children.Add(line);

            AddPage(canvas, node, startX, nextY, nodeLevels);

            startX += PageWidth + HorizontalMargin;
        }
    }

    public IndexControl()
    {
        InitializeComponent();

        IndexScrollViewer.PointerWheelChanged += Index_PointerWheelChanged;

        //pagePaint = new SKPaint
        //{
        //    Style = SKPaintStyle.Fill,
        //    Color = SKColors.Gray,
        //    IsAntialias = true,
        //    StrokeWidth = 1
        //};

        //linePaint = new SKPaint
        //{
        //    Style = SKPaintStyle.Stroke,
        //    Color = SKColors.IndianRed,
        //    IsAntialias = true,
        //    StrokeWidth = 0.5f
        //};
    }

    //private void IndexCanvas_PaintSurface(object sender, SkiaSharp.Views.Windows.SKPaintSurfaceEventArgs e)
    //{
    //    Draw(e.Surface.Canvas, RootNode, (IndexCanvas.CanvasSize.Width / 2) - (pageWidth / 2), 50);
    //}

    //private void Draw(SKCanvas canvas, IndexNode root, float x, float y)
    //{
    //    // Draw the box for the current node
    //    var rect = new SKRect(x, y, x + pageWidth, y + pageHeight);

    //    canvas.DrawRect(rect, pagePaint);

    //    var nextY = y + pageHeight + verticalMargin;

    //    var siblingCount = root.Children.Count; 

    //    var totalWidth = siblingCount * pageWidth + (siblingCount - 1) * horizontalMargin;

    //    var startX = (canvas.LocalClipBounds.Width / 2) - (totalWidth / 2);

    //    // Draw each subordinate
    //    foreach (var node in root.Children)
    //    {
    //        // Draw a line to the subordinate
    //        canvas.DrawLine(x + pageWidth / 2, y + pageHeight, startX + pageWidth / 2, nextY, linePaint);

    //        // Draw the subordinate node
    //        Draw(canvas, node, startX, nextY);

    //        // Update the X coordinate for the next subordinate
    //        startX += pageWidth + horizontalMargin;
    //    }
    //}
}

public class ViewIndexEventArgs(long allocationUnitId) : EventArgs
{
    public long AllocationUnitId { get; } = allocationUnitId;
}