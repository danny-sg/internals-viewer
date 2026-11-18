using System;
using System.Linq;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Memory;

public sealed partial class MemoryUsageChartControl : SKXamlCanvas
{
    private const float PlotMargin = 8f;

    private const float LabelHeight = 18f;

    private const float LabelPadding = 4f;

    private const float TotalWidth = 2f;

    private const float SeriesWidth = 1.25f;

    private const float MarkerRadius = 3f;

    private const float FontSize = 12f;

    private const float LegendSample = 14f;

    private const float LegendGap = 12f;

    private const float HoverDistance = 6f;

    private const string PlayheadKey = "SystemAccentColorDark1";

    public static readonly DependencyProperty ChartProperty =
        DependencyProperty.Register(nameof(Chart),
                                    typeof(MemoryUsageChart),
                                    typeof(MemoryUsageChartControl),
                                    new PropertyMetadata(null, OnChartChanged));

    public MemoryUsageChart? Chart
    {
        get => (MemoryUsageChart?)GetValue(ChartProperty);
        set => SetValue(ChartProperty, value);
    }

    private static readonly SKColor BackgroundColour = SKColors.White;

    private static readonly SKColor LabelColour = new(0x00, 0x00, 0x00, 0xE4);

    private static readonly SKColor GuideColour = new(0xB8, 0xB8, 0xB8);

    private static readonly SKTypeface LabelTypeface = SKTypeface.FromFamilyName("Segoe UI Variable Text") ?? SKTypeface.Default;

    private SKPaint? _line;

    private SKPaint? _fill;

    private SKFont? _font;

    private SKPathBuilder? _pathBuilder;

    private SKColor? _playheadColour;

    public MemoryUsageChartControl()
    {
        IgnorePixelScaling = true;

        PaintSurface += OnPaintSurface;

        PointerMoved += OnPointerMoved;

        PointerExited += OnPointerExited;

        PointerPressed += OnPointerPressed;

        ActualThemeChanged += (_, _) =>
        {
            _playheadColour = null;

            Invalidate();
        };

        Unloaded += (_, _) =>
        {
            _line?.Dispose();
            _line = null;

            _fill?.Dispose();
            _fill = null;

            _font?.Dispose();
            _font = null;

            _pathBuilder?.Dispose();
            _pathBuilder = null;
        };
    }

    public event EventHandler<double>? PositionRequested;

    private SKPoint? HoverPoint { get; set; }

    private static SKColor ResolveColour(string key, SKColor fallback)
    {
        if (Application.Current.Resources.TryGetValue(key, out var value) && value is Windows.UI.Color colour)
        {
            return new SKColor(colour.R, colour.G, colour.B, colour.A);
        }

        return fallback;
    }

    private static SKColor GetColour(MemoryUsageSeries series) => new(series.Colour.R, series.Colour.G, series.Colour.B, series.Colour.A);

    private static SKRect GetPlot(float width, float height)
        => new(PlotMargin,
               PlotMargin + LabelHeight,
               Math.Max(PlotMargin + 1, width - PlotMargin),
               Math.Max(PlotMargin + LabelHeight + 1, height - PlotMargin));

    private static float GetX(SKRect plot, int index, int count) => plot.Left + (index + 0.5f) / count * plot.Width;

    private static float GetY(SKRect plot, ulong value, ulong peak)
        => peak == 0 ? plot.Bottom : plot.Bottom - (float)((double)value / peak) * plot.Height;

    private static MemoryUsageSeries? GetHoverSeries(MemoryUsageChart chart, SKRect plot, int index, float y, ulong peak)
    {
        MemoryUsageSeries? nearest = null;

        var distance = HoverDistance;

        foreach (var series in chart.Series.Where(s => index < s.Values.Length))
        {
            var gap = Math.Abs(GetY(plot, series.Values[index], peak) - y);

            if (gap <= HoverDistance && (nearest is null || gap < distance))
            {
                nearest = series;

                distance = gap;
            }
        }

        return nearest;
    }

    private static void OnChartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MemoryUsageChartControl)d).Invalidate();

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(this).Position;

        HoverPoint = new SKPoint((float)position.X, (float)position.Y);

        Invalidate();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        HoverPoint = null;

        Invalidate();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);

        if (point.PointerDeviceType == PointerDeviceType.Mouse && !point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var plot = GetPlot((float)ActualWidth, (float)ActualHeight);

        if (plot.Width > 0)
        {
            PositionRequested?.Invoke(this, Math.Clamp((point.Position.X - plot.Left) / plot.Width, 0, 1));
        }
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        canvas.Clear(BackgroundColour);

        if (Chart is not { Series.Count: > 0 } chart || chart.Series[0].Values.Length == 0)
        {
            return;
        }

        _line ??= new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeJoin = SKStrokeJoin.Round };

        _fill ??= new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        _font ??= new SKFont(LabelTypeface, FontSize);

        _playheadColour ??= ResolveColour(PlayheadKey, SKColors.DodgerBlue);

        var plot = GetPlot(e.Info.Width, e.Info.Height);

        var peak = Math.Max(1, chart.Series.Max(s => s.Values.Length == 0 ? 0 : s.Values.Max()));

        _fill.Color = LabelColour;

        canvas.DrawText($"Peak {StringHelpers.FormatSize((long)chart.Peak)}",
                        plot.Left,
                        PlotMargin + FontSize,
                        SKTextAlign.Left,
                        _font,
                        _fill);

        DrawLegend(canvas, chart, plot);

        _line.Color = GuideColour;

        _line.StrokeWidth = 1;

        canvas.DrawLine(plot.Left, plot.Bottom, plot.Right, plot.Bottom, _line);

        foreach (var series in chart.Series.Reverse())
        {
            DrawSeries(canvas, plot, series, peak);
        }

        if (chart.Playhead is >= 0 and <= 1)
        {
            var x = plot.Left + (float)chart.Playhead * plot.Width;

            _line.Color = _playheadColour.Value;

            _line.StrokeWidth = 1;

            canvas.DrawLine(x, plot.Top, x, plot.Bottom, _line);
        }

        if (HoverPoint is { } hover && hover.X >= plot.Left && hover.X <= plot.Right)
        {
            DrawHover(canvas, plot, chart, hover, peak);
        }
    }

    private void DrawLegend(SKCanvas canvas, MemoryUsageChart chart, SKRect plot)
    {
        var right = plot.Right;

        var y = PlotMargin + FontSize / 2;

        foreach (var series in chart.Series.Reverse())
        {
            var width = LegendSample + LabelPadding + _font!.MeasureText(series.Name);

            var left = right - width;

            if (left < plot.Left + plot.Width / 3)
            {
                break;
            }

            _line!.Color = GetColour(series);

            _line.StrokeWidth = series.IsTotal ? TotalWidth : SeriesWidth;

            canvas.DrawLine(left, y, left + LegendSample, y, _line);

            _fill!.Color = LabelColour;

            canvas.DrawText(series.Name, left + LegendSample + LabelPadding, PlotMargin + FontSize, SKTextAlign.Left, _font, _fill);

            right = left - LegendGap;
        }
    }

    private void DrawSeries(SKCanvas canvas, SKRect plot, MemoryUsageSeries series, ulong peak)
    {
        var values = series.Values;

        _pathBuilder ??= new SKPathBuilder();

        _pathBuilder.MoveTo(GetX(plot, 0, values.Length), GetY(plot, values[0], peak));

        for (var index = 1; index < values.Length; index++)
        {
            _pathBuilder.LineTo(GetX(plot, index, values.Length), GetY(plot, values[index], peak));
        }

        using var path = _pathBuilder.Detach();

        _line!.Color = GetColour(series);

        _line.StrokeWidth = series.IsTotal ? TotalWidth : SeriesWidth;

        canvas.DrawPath(path, _line);
    }

    private void DrawHover(SKCanvas canvas, SKRect plot, MemoryUsageChart chart, SKPoint hover, ulong peak)
    {
        var count = chart.Series[0].Values.Length;

        var index = Math.Clamp((int)((hover.X - plot.Left) / plot.Width * count), 0, count - 1);

        var x = GetX(plot, index, count);

        _line!.Color = GuideColour;

        _line.StrokeWidth = 1;

        canvas.DrawLine(x, plot.Top, x, plot.Bottom, _line);

        if (GetHoverSeries(chart, plot, index, hover.Y, peak) is not { } series)
        {
            return;
        }

        var value = series.Values[index];

        var y = GetY(plot, value, peak);

        _fill!.Color = GetColour(series);

        canvas.DrawCircle(x, y, MarkerRadius, _fill);

        var text = $"{series.Name} {StringHelpers.FormatSize((long)value)}";

        var width = _font!.MeasureText(text) + LabelPadding * 2;

        var left = x + MarkerRadius + LabelPadding + width > plot.Right
            ? x - MarkerRadius - LabelPadding - width
            : x + MarkerRadius + LabelPadding;

        var top = Math.Clamp(y - LabelHeight / 2, plot.Top, plot.Bottom - LabelHeight);

        var box = new SKRect(left, top, left + width, top + LabelHeight);

        _fill.Color = BackgroundColour;

        canvas.DrawRect(box, _fill);

        canvas.DrawRect(box, _line);

        _fill.Color = LabelColour;

        canvas.DrawText(text, left + LabelPadding, top + LabelHeight - LabelPadding - 1, SKTextAlign.Left, _font, _fill);
    }
}
