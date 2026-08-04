using System;
using System.Collections.Generic;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace InternalsViewer.UI.App.Controls.AccessSteps;

public sealed partial class HashGrid : SKXamlCanvas
{
    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(nameof(Fill),
                                    typeof(IReadOnlyList<int>),
                                    typeof(HashGrid),
                                    new PropertyMetadata(null, OnFillChanged));

    public IReadOnlyList<int>? Fill
    {
        get => (IReadOnlyList<int>?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public static readonly DependencyProperty VersionProperty =
        DependencyProperty.Register(nameof(Version),
                                    typeof(int),
                                    typeof(HashGrid),
                                    new PropertyMetadata(0, OnFillChanged));

    public int Version
    {
        get => (int)GetValue(VersionProperty);
        set => SetValue(VersionProperty, value);
    }

    public static readonly DependencyProperty HighlightBucketProperty =
        DependencyProperty.Register(nameof(HighlightBucket),
                                    typeof(int),
                                    typeof(HashGrid),
                                    new PropertyMetadata(-1, OnFillChanged));

    public int HighlightBucket
    {
        get => (int)GetValue(HighlightBucketProperty);
        set => SetValue(HighlightBucketProperty, value);
    }

    public static readonly DependencyProperty IsHighlightMatchProperty =
        DependencyProperty.Register(nameof(IsHighlightMatch),
                                    typeof(bool),
                                    typeof(HashGrid),
                                    new PropertyMetadata(false, OnFillChanged));

    public bool IsHighlightMatch
    {
        get => (bool)GetValue(IsHighlightMatchProperty);
        set => SetValue(IsHighlightMatchProperty, value);
    }

    private const int PaintIntervalMs = 100;

    private DispatcherQueueTimer? _throttle;

    private long _lastPaint;

    private SKPaint? _paint;

    public HashGrid()
    {
        IgnorePixelScaling = true;

        PaintSurface += OnPaintSurface;

        DataContextChanged += (_, _) => Invalidate();

        Unloaded += (_, _) =>
        {
            _throttle?.Stop();

            _paint?.Dispose();
            _paint = null;
        };
    }

    private static void OnFillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((HashGrid)d).RequestInvalidate();
    }

    private void RequestInvalidate()
    {
        var now = Environment.TickCount64;

        if (now - _lastPaint >= PaintIntervalMs)
        {
            _lastPaint = now;

            Invalidate();

            return;
        }

        if (_throttle is null)
        {
            _throttle = DispatcherQueue.CreateTimer();

            _throttle.Interval = TimeSpan.FromMilliseconds(PaintIntervalMs);
            _throttle.IsRepeating = false;

            _throttle.Tick += (_, _) =>
            {
                _lastPaint = Environment.TickCount64;

                Invalidate();
            };
        }

        if (!_throttle.IsRunning)
        {
            _throttle.Start();
        }
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

        var paint = _paint ??= new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Fill
        };

        var gap = cell > 2 ? 1 : 0;

        for (var index = 0; index < fill.Count; index++)
        {
            var x = index % columns * cell;
            var y = index / columns * cell;

            var count = fill[index];

            if (index == HighlightBucket)
            {
                paint.Color = IsHighlightMatch ? new SKColor(15, 123, 15) : new SKColor(0, 0, 64);
            }
            else
            {
                paint.Color = count == 0
                    ? new SKColor(128, 128, 128, 24)
                    : new SKColor(0, 0, 128, (byte)(70 + (185 * Math.Min(count, max) / max)));
            }

            canvas.DrawRect(x, y, cell - gap, cell - gap, paint);
        }
    }
}
