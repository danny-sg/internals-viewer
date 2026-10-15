using System;
using Windows.Foundation;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.FlameChart;

public sealed partial class FlameChartControl
{
    private const float RowBufferBarHeight = 9f;

    private const float RowBufferMarkWidth = 2f;

    private static readonly SKColor RowBufferMarkColour = new(0xFF, 0x99, 0xA4);

    private static readonly SKColor BatchMarkColour = new(0x8C, 0xC8, 0xFF);

    public static readonly DependencyProperty RowBufferMarksProperty =
        DependencyProperty.Register(nameof(RowBufferMarks),
                                    typeof(TraceRowBufferMarks),
                                    typeof(FlameChartControl),
                                    new PropertyMetadata(null, OnRowBuffersChanged));

    public TraceRowBufferMarks? RowBufferMarks
    {
        get => (TraceRowBufferMarks?)GetValue(RowBufferMarksProperty);
        set => SetValue(RowBufferMarksProperty, value);
    }

    public static readonly DependencyProperty ShowRowBuffersProperty =
        DependencyProperty.Register(nameof(ShowRowBuffers),
                                    typeof(bool),
                                    typeof(FlameChartControl),
                                    new PropertyMetadata(true, OnRowBuffersChanged));

    public bool ShowRowBuffers
    {
        get => (bool)GetValue(ShowRowBuffersProperty);
        set => SetValue(ShowRowBuffersProperty, value);
    }

    private bool ShowsRowBuffers => ShowRowBuffers && RowBufferMarks is { Count: > 0 };

    private float RowsOffset => LaneHeaderHeight + (ShowsRowBuffers ? RowBufferBarHeight : 0);

    private void DrawRowBufferBar(SKCanvas canvas, TraceTimelineThread thread, float top, int width, double scale)
    {
        if (!ShowsRowBuffers || RowBufferMarks is not { } marks)
        {
            return;
        }

        _paints.Fill.Color = OperatorPaneColour;

        canvas.DrawRect(0, top, width, RowBufferBarHeight, _paints.Fill);

        DrawMarks(canvas, marks.GetMarks(thread.ThreadId, _axis), top, scale, RowBufferMarkColour);

        DrawMarks(canvas, marks.GetBatchMarks(thread.ThreadId, _axis), top, scale, BatchMarkColour);
    }

    private void DrawMarks(SKCanvas canvas, ReadOnlySpan<double> values, float top, double scale, SKColor colour)
    {
        if (values.IsEmpty)
        {
            return;
        }

        _paints.Fill.Color = colour;

        var index = SortedSearch.FirstAtOrAfter(values, _viewStart);

        while (index < values.Length && values[index] <= _viewEnd)
        {
            var x = (float)((values[index] - _viewStart) * scale);

            canvas.DrawRect(x, top + 1, RowBufferMarkWidth, RowBufferBarHeight - 2, _paints.Fill);

            index = SortedSearch.FirstAtOrAfter(values, _viewStart + (Math.Floor(x) + RowBufferMarkWidth) / scale, index + 1);
        }
    }

    private bool IsOnRowBufferBar(Point position) => GetRowBufferLane(position) >= 0;

    private (CallStackNode Node, int Call)? GetRowBufferMarkAt(Point position)
    {
        if (GetRowBufferLane(position) is not (>= 0 and var lane)
            || _visible is not { } timeline
            || RowBufferMarks is not { } marks)
        {
            return null;
        }

        var scale = PixelsPerUnit((int)_overlay.ActualWidth);

        return marks.GetMarkAt(timeline.Threads[lane].ThreadId,
                               _axis,
                               _viewStart + position.X / scale,
                               (HitTolerance + RowBufferMarkWidth) / scale);
    }

    private int GetRowBufferLane(Point position)
    {
        if (!ShowsRowBuffers || _visible is not { } timeline || position.Y < ContentTop)
        {
            return -1;
        }

        var contentY = position.Y - ContentTop + _scrollY;

        for (var lane = 0; lane < timeline.Threads.Count && lane < _laneTops.Length; lane++)
        {
            var top = _laneTops[lane] + LaneHeaderHeight;

            if (_laneTops[lane] + RowsOffset >= _scrollY && contentY >= top && contentY < top + RowBufferBarHeight)
            {
                return lane;
            }
        }

        return -1;
    }

    private static void OnRowBuffersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FlameChartControl)d).Relayout();
}
