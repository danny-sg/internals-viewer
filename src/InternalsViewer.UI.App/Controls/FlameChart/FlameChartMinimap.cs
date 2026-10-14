using System;
using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace InternalsViewer.UI.App.Controls.FlameChart;

public sealed class FlameChartMinimap : UserControl, IDisposable
{
    private const float Padding = 3f;

    private const float Radius = 3f;

    private const float RunGap = 0.5f;

    private const float MinimumWindowWidth = 2f;

    private const float Saturation = 0.45f;

    private const float Fade = 0.5f;

    public static readonly DependencyProperty TimelineProperty =
        DependencyProperty.Register(nameof(Timeline),
                                    typeof(TraceTimeline),
                                    typeof(FlameChartMinimap),
                                    new PropertyMetadata(null, OnChanged));

    public TraceTimeline? Timeline
    {
        get => (TraceTimeline?)GetValue(TimelineProperty);
        set => SetValue(TimelineProperty, value);
    }

    public static readonly DependencyProperty AxisProperty =
        DependencyProperty.Register(nameof(Axis),
                                    typeof(TraceTimelineAxis),
                                    typeof(FlameChartMinimap),
                                    new PropertyMetadata(TraceTimelineAxis.Position, OnChanged));

    public TraceTimelineAxis Axis
    {
        get => (TraceTimelineAxis)GetValue(AxisProperty);
        set => SetValue(AxisProperty, value);
    }

    public static readonly DependencyProperty WindowStartProperty =
        DependencyProperty.Register(nameof(WindowStart),
                                    typeof(double),
                                    typeof(FlameChartMinimap),
                                    new PropertyMetadata(double.NaN, OnChanged));

    public double WindowStart
    {
        get => (double)GetValue(WindowStartProperty);
        set => SetValue(WindowStartProperty, value);
    }

    public static readonly DependencyProperty WindowEndProperty =
        DependencyProperty.Register(nameof(WindowEnd),
                                    typeof(double),
                                    typeof(FlameChartMinimap),
                                    new PropertyMetadata(double.NaN, OnChanged));

    public double WindowEnd
    {
        get => (double)GetValue(WindowEndProperty);
        set => SetValue(WindowEndProperty, value);
    }

    public static readonly DependencyProperty ScopeProperty =
        DependencyProperty.Register(nameof(Scope),
                                    typeof(TraceTimeline),
                                    typeof(FlameChartMinimap),
                                    new PropertyMetadata(null, OnChanged));

    public TraceTimeline? Scope
    {
        get => (TraceTimeline?)GetValue(ScopeProperty);
        set => SetValue(ScopeProperty, value);
    }

    public static readonly DependencyProperty CropProperty =
        DependencyProperty.Register(nameof(Crop),
                                    typeof(TraceTimeline),
                                    typeof(FlameChartMinimap),
                                    new PropertyMetadata(null, OnChanged));

    public TraceTimeline? Crop
    {
        get => (TraceTimeline?)GetValue(CropProperty);
        set => SetValue(CropProperty, value);
    }

    public static readonly DependencyProperty MarkersProperty =
        DependencyProperty.Register(nameof(Markers),
                                    typeof(IReadOnlyList<MinimapMarker>),
                                    typeof(FlameChartMinimap),
                                    new PropertyMetadata(null, OnChanged));

    public IReadOnlyList<MinimapMarker>? Markers
    {
        get => (IReadOnlyList<MinimapMarker>?)GetValue(MarkersProperty);
        set => SetValue(MarkersProperty, value);
    }

    private readonly SKXamlCanvas _canvas;

    private readonly SKPaint _background = new() { Style = SKPaintStyle.Fill, IsAntialias = true };

    private readonly SKPaint _run = new() { Style = SKPaintStyle.Fill, IsAntialias = true };

    private readonly SKPaint _window = new() { Style = SKPaintStyle.Fill };

    private readonly SKPaint _marker = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2f };

    private readonly Dictionary<(string Hex, bool IsGrey), SKColor> _colours = [];

    private readonly SKColor _unknown = new(128, 128, 128);

    private SKColor _fadeTo = new(0xF3, 0xF3, 0xF3);

    private SKPicture? _picture;

    private (TraceTimeline Timeline, TraceTimeline? Crop, TraceTimelineAxis Axis, int Width, int Height)? _pictureKey;

    public FlameChartMinimap()
    {
        _canvas = new SKXamlCanvas { IgnorePixelScaling = false };

        _canvas.PaintSurface += OnPaintSurface;

        Content = _canvas;

        IsHitTestVisible = false;

        Loaded += OnLoaded;

        ActualThemeChanged += OnActualThemeChanged;
    }

    public void Dispose()
    {
        Loaded -= OnLoaded;

        ActualThemeChanged -= OnActualThemeChanged;

        _canvas.PaintSurface -= OnPaintSurface;

        _picture?.Dispose();
        _picture = null;

        _background.Dispose();
        _run.Dispose();
        _window.Dispose();
        _marker.Dispose();
    }

    private static float GetMarkerTop(TraceTimeline timeline, MinimapMarker marker, float height)
    {
        var threads = timeline.Threads;

        if (marker.Lane < 0 || marker.Lane >= threads.Count || marker.Depth < 0)
        {
            return 0;
        }

        var laneHeight = height / threads.Count;

        var rowHeight = laneHeight / Math.Max(1, threads[marker.Lane].Rows.Count);

        return Math.Min(height, marker.Lane * laneHeight + marker.Depth * rowHeight);
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => ApplyTheme();

    private void OnActualThemeChanged(FrameworkElement sender, object args) => ApplyTheme();

    private void ApplyTheme()
    {
        var isDark = ActualTheme == ElementTheme.Dark;

        _background.Color = isDark ? new SKColor(0x20, 0x20, 0x20, 0xD9) : new SKColor(0xF3, 0xF3, 0xF3, 0xD9);
        _window.Color = isDark ? new SKColor(110, 180, 255, 60) : new SKColor(0, 103, 192, 50);

        _fadeTo = _background.Color.WithAlpha(255);

        _colours.Clear();

        _picture?.Dispose();
        _picture = null;

        _canvas.Invalidate();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        canvas.Clear(SKColors.Transparent);

        var scale = (float)_canvas.Dpi;

        canvas.Scale(scale);

        var width = e.Info.Width / scale;

        var height = e.Info.Height / scale;

        var innerWidth = width - Padding * 2;

        var innerHeight = height - Padding * 2;

        if (innerWidth <= 0 || innerHeight <= 0)
        {
            return;
        }

        canvas.DrawRoundRect(new SKRect(0, 0, width, height), Radius, Radius, _background);

        if (Timeline is { } timeline)
        {
            canvas.Save();

            canvas.Translate(Padding, Padding);

            canvas.ClipRect(new SKRect(0, 0, innerWidth, innerHeight));

            DrawContent(canvas, timeline, innerWidth, innerHeight);

            canvas.Restore();
        }
    }

    private void DrawContent(SKCanvas canvas, TraceTimeline timeline, float width, float height)
    {
        var key = (timeline, Crop, Axis, (int)width, (int)height);

        if (_picture is null || _pictureKey != key)
        {
            _picture?.Dispose();

            _picture = Record(timeline, width, height);

            _pictureKey = key;
        }

        canvas.DrawPicture(_picture);

        var start = timeline.StartOf(Axis);

        var unit = width / Math.Max(timeline.EndOf(Axis) - start, double.Epsilon);

        if (!double.IsNaN(WindowStart) && !double.IsNaN(WindowEnd))
        {
            if (Scope is { } scope)
            {
                DrawScopedWindow(canvas, timeline, scope, start, unit, height);
            }
            else
            {
                var left = (float)((WindowStart - start) * unit);

                var right = Math.Max((float)((WindowEnd - start) * unit), left + MinimumWindowWidth);

                canvas.DrawRect(new SKRect(left, 0, right, height), _window);
            }
        }

        foreach (var marker in Markers ?? [])
        {
            _marker.Color = new SKColor(marker.Colour.R, marker.Colour.G, marker.Colour.B, marker.Colour.A);

            var x = (float)((marker.Position - start) * unit);

            canvas.DrawLine(x, GetMarkerTop(timeline, marker, height), x, height, _marker);
        }
    }

    private void DrawScopedWindow(SKCanvas canvas, TraceTimeline timeline, TraceTimeline scope, double start, double unit, float height)
    {
        var lanes = timeline.Threads;

        var laneHeight = height / Math.Max(1, lanes.Count);

        foreach (var thread in scope.Threads)
        {
            if (thread.Rows.Count == 0)
            {
                continue;
            }

            var lane = -1;

            for (var index = 0; index < lanes.Count; index++)
            {
                if (lanes[index].ThreadId == thread.ThreadId)
                {
                    lane = index;

                    break;
                }
            }

            if (lane < 0)
            {
                continue;
            }

            DrawWindowRuns(canvas, thread.Rows[0], start, unit, lane * laneHeight, laneHeight);
        }
    }

    private void DrawWindowRuns(SKCanvas canvas, TraceTimelineRow row, double start, double unit, float top, float height)
    {
        var starts = row.Starts(Axis);

        var ends = row.Ends(Axis);

        var runLeft = 0f;

        var runRight = -1f;

        for (var index = row.FirstEndingAfter(Axis, WindowStart); index < row.Count && starts[index] < WindowEnd; index++)
        {
            var left = (float)((Math.Max(starts[index], WindowStart) - start) * unit);

            var right = Math.Max((float)((Math.Min(ends[index], WindowEnd) - start) * unit), left + MinimumWindowWidth);

            if (runRight >= 0 && left <= runRight + RunGap)
            {
                runRight = Math.Max(runRight, right);

                continue;
            }

            if (runRight >= 0)
            {
                canvas.DrawRect(new SKRect(runLeft, top, runRight, top + height), _window);
            }

            runLeft = left;

            runRight = right;
        }

        if (runRight >= 0)
        {
            canvas.DrawRect(new SKRect(runLeft, top, runRight, top + height), _window);
        }
    }

    private SKPicture Record(TraceTimeline timeline, float width, float height)
    {
        using var recorder = new SKPictureRecorder();

        var canvas = recorder.BeginRecording(new SKRect(0, 0, width, height));

        var threads = timeline.Threads;

        if (threads.Count == 0)
        {
            return recorder.EndRecording();
        }

        var start = timeline.StartOf(Axis);

        var unit = width / Math.Max(timeline.EndOf(Axis) - start, double.Epsilon);

        var laneHeight = height / threads.Count;

        var included = Crop is { } crop ? GetIncludedNodes(crop) : null;

        for (var lane = 0; lane < threads.Count; lane++)
        {
            var rows = threads[lane].Rows;

            var rowHeight = laneHeight / Math.Max(1, rows.Count);

            for (var depth = 0; depth < rows.Count; depth++)
            {
                DrawRow(canvas, timeline, rows[depth], included, lane * laneHeight + depth * rowHeight, rowHeight, start, unit);
            }
        }

        return recorder.EndRecording();
    }

    private void DrawRow(SKCanvas canvas,
                         TraceTimeline timeline,
                         TraceTimelineRow row,
                         bool[]? included,
                         float top,
                         float height,
                         double start,
                         double unit)
    {
        var starts = row.Starts(Axis);

        var ends = row.Ends(Axis);

        var runLeft = 0f;

        var runRight = 0f;

        var runNode = -1;

        var runGrey = false;

        for (var index = 0; index < row.Count; index++)
        {
            var left = (float)((starts[index] - start) * unit);

            var right = (float)((ends[index] - start) * unit);

            var node = row.NodeAt(index);

            var isGrey = included is not null && (node < 0 || node >= included.Length || !included[node]);

            if (runNode >= 0 && isGrey == runGrey && left <= runRight + RunGap)
            {
                runRight = Math.Max(runRight, right);

                continue;
            }

            if (runNode >= 0)
            {
                FillRun(canvas, timeline, runNode, runGrey, runLeft, runRight, top, height);
            }

            runLeft = left;

            runRight = right;

            runNode = node;

            runGrey = isGrey;
        }

        if (runNode >= 0)
        {
            FillRun(canvas, timeline, runNode, runGrey, runLeft, runRight, top, height);
        }
    }

    private static bool[] GetIncludedNodes(TraceTimeline crop)
    {
        var included = new bool[crop.NodeCount];

        foreach (var thread in crop.Threads)
        {
            foreach (var row in thread.Rows)
            {
                for (var index = 0; index < row.Count; index++)
                {
                    var node = row.NodeAt(index);

                    if (node >= 0 && node < included.Length)
                    {
                        included[node] = true;
                    }
                }
            }
        }

        return included;
    }

    private void FillRun(SKCanvas canvas,
                         TraceTimeline timeline,
                         int node,
                         bool isGrey,
                         float left,
                         float right,
                         float top,
                         float height)
    {
        _run.Color = ColourOf(timeline.NodeOf(node)?.CategoryColour, isGrey);

        canvas.DrawRect(new SKRect(left, top, Math.Max(right, left + RunGap), top + height), _run);
    }

    private SKColor ColourOf(string? hex, bool isGrey)
    {
        if (hex is null)
        {
            return Muted(_unknown, isGrey);
        }

        if (!_colours.TryGetValue((hex, isGrey), out var colour))
        {
            colour = Muted(SKColor.TryParse(hex, out var parsed) ? parsed : _unknown, isGrey);

            _colours[(hex, isGrey)] = colour;
        }

        return colour;
    }

    private SKColor Muted(SKColor colour, bool isGrey)
    {
        colour.ToHsl(out var hue, out var saturation, out var lightness);

        var desaturated = SKColor.FromHsl(hue, isGrey ? 0 : saturation * Saturation, lightness);

        return new SKColor(Blend(desaturated.Red, _fadeTo.Red),
                           Blend(desaturated.Green, _fadeTo.Green),
                           Blend(desaturated.Blue, _fadeTo.Blue));
    }

    private static byte Blend(byte from, byte to) => (byte)(from + (to - from) * Fade);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FlameChartMinimap)d)._canvas.Invalidate();
}
