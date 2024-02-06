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

namespace InternalsViewer.UI.App.Controls.Index;

public sealed partial class IndexControl
{
    private SKPaint pagePaint;
    private SKPaint linePaint;
    private float pageWidth = 5;
    private float pageHeight = 10;
    private float horizontalMargin = 20;
    private float verticallMargin = 50;

    public IndexNode RootNode { get; set; }

    public static readonly DependencyProperty IndexNodeProperty = DependencyProperty.Register(nameof(RootNode),
    typeof(IndexNode),
    typeof(IndexControl),
    null);

    public IndexControl()
    {
        this.InitializeComponent();

        pagePaint = new SKPaint
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
            StrokeWidth = 0.5f
        };
    }

    private void IndexCanvas_PaintSurface(object sender, SkiaSharp.Views.Windows.SKPaintSurfaceEventArgs e)
    {
        Draw(e.Surface.Canvas, RootNode, (IndexCanvas.CanvasSize.Width / 2) - (pageWidth / 2), 50);
    }

    private void Draw(SKCanvas canvas, IndexNode root, float x, float y)
    {
        // Draw the box for the current node
        var rect = new SKRect(x, y, x + pageWidth, y + pageHeight);

        canvas.DrawRect(rect, pagePaint);

        var nextY = y + pageHeight + verticallMargin;
     
        var siblingCount = root.Children.Count; 

        var totalWidth = siblingCount * pageWidth + (siblingCount - 1) * horizontalMargin;

        var startX = (canvas.LocalClipBounds.Width / 2) - (totalWidth / 2);
      
        // Draw each subordinate
        foreach (var node in root.Children)
        {
            // Draw a line to the subordinate
            canvas.DrawLine(x + pageWidth / 2, y + pageHeight, startX + pageWidth / 2, nextY, linePaint);

            // Draw the subordinate node
            Draw(canvas, node, startX, nextY);

            // Update the X coordinate for the next subordinate
            startX += pageWidth + horizontalMargin;
        }
    }
}

public class ViewIndexEventArgs(long allocationUnitId) : EventArgs
{
    public long AllocationUnitId { get; } = allocationUnitId;
}