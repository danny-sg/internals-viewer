using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace InternalsViewer.UI.App.Controls.AccessSteps;

public sealed partial class HashFillGrid : SKXamlCanvas
{
    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(nameof(Fill),
                                    typeof(IReadOnlyList<int>),
                                    typeof(HashFillGrid),
                                    new PropertyMetadata(null, OnFillChanged));

    public IReadOnlyList<int>? Fill
    {
        get => (IReadOnlyList<int>?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public HashFillGrid()
    {
        IgnorePixelScaling = true;

        PaintSurface += OnPaintSurface;

        DataContextChanged += (_, _) => Invalidate();
    }

    private static void OnFillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((HashFillGrid)d).Invalidate();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        canvas.Clear(SKColors.Transparent);

        if (Fill is not { Count: > 0 } fill)
        {
            return;
        }

        var width = e.Info.Width;
        var height = e.Info.Height;

        var cell = 1;

        for (var size = 8; size >= 2; size--)
        {
            var fitColumns = Math.Max(1, width / size);

            var fitRows = (fill.Count + fitColumns - 1) / fitColumns;

            if (fitRows * size <= height)
            {
                cell = size;

                break;
            }
        }

        var columns = Math.Max(1, width / cell);

        var max = 1;

        for (var index = 0; index < fill.Count; index++)
        {
            if (fill[index] > max)
            {
                max = fill[index];
            }
        }

        using var paint = new SKPaint();

        paint.IsAntialias = false;
        paint.Style = SKPaintStyle.Fill;

        var gap = cell > 2 ? 1 : 0;

        for (var index = 0; index < fill.Count; index++)
        {
            var x = index % columns * cell;
            var y = index / columns * cell;

            var count = fill[index];

            paint.Color = count == 0
                ? new SKColor(128, 128, 128, 24)
                : new SKColor(0, 0, 128, (byte)(70 + (185 * Math.Min(count, max) / max)));

            canvas.DrawRect(x, y, cell - gap, cell - gap, paint);
        }
    }
}
