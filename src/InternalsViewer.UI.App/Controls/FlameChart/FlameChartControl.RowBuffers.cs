using System;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.FlameChart;

public sealed partial class FlameChartControl
{
    private const float RowBufferBarHeight = 9f;

    private const float RowBufferMarkWidth = 2f;

    private static readonly SKColor RowBufferMarkColour = new(0xFF, 0x99, 0xA4);

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

        var values = marks.GetMarks(thread.ThreadId, _axis);

        if (values.IsEmpty)
        {
            return;
        }

        _paints.Fill.Color = RowBufferMarkColour;

        var index = SortedSearch.FirstAtOrAfter(values, _viewStart);

        while (index < values.Length && values[index] <= _viewEnd)
        {
            var x = (float)((values[index] - _viewStart) * scale);

            canvas.DrawRect(x, top + 1, RowBufferMarkWidth, RowBufferBarHeight - 2, _paints.Fill);

            index = SortedSearch.FirstAtOrAfter(values, _viewStart + (Math.Floor(x) + RowBufferMarkWidth) / scale, index + 1);
        }
    }

    private static void OnRowBuffersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FlameChartControl)d).Relayout();
}
