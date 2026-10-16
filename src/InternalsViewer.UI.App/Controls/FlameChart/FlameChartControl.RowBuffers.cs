using System;
using Windows.Foundation;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.FlameChart;

public sealed partial class FlameChartControl
{
    private const float MarkBandHeight = 9f;

    private const float MarkBandShare = 0.4f;

    private const float RowBufferMarkWidth = 2f;

    private const string MarkBandToolTip = "Row Buffers/Batches";

    private static readonly SKColor RowBufferMarkColour = new(0xFF, 0x99, 0xA4);

    private static readonly SKColor BatchMarkColour = new(0x8C, 0xC8, 0xFF);

    private static readonly SKColor MarkBandColour = new(48, 48, 48);

    private static readonly SKColor MarkBandHoverColour = new(78, 78, 78);

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

    private int? _hoverMarkBand;

    public event Action<CallStackNode, int, bool>? RowBufferOpenRequested;

    private bool HasMarkBand(int row)
        => ShowRowBuffers && RowBufferMarks is { Count: > 0 } marks && marks.HasMarks(_operatorRows[row].Operator);

    private float MarkBandHeightOf(int row) => HasMarkBand(row) ? Math.Min(MarkBandHeight, OperatorRowHeight * MarkBandShare) : 0;

    private void DrawMarkBand(SKCanvas canvas, int row, int width, double scale)
    {
        var height = MarkBandHeightOf(row);

        if (height <= 0 || RowBufferMarks is not { } marks)
        {
            return;
        }

        var top = OperatorRowTop(row) + OperatorRowHeight - height;

        _paints.Fill.Color = _hoverMarkBand == row ? MarkBandHoverColour : MarkBandColour;

        canvas.DrawRect(0, top, width, height, _paints.Fill);

        var operatorEvent = _operatorRows[row].Operator;

        DrawMarks(canvas, marks.GetMarks(operatorEvent, _axis), top, height, scale, RowBufferMarkColour);

        DrawMarks(canvas, marks.GetBatchMarks(operatorEvent, _axis), top, height, scale, BatchMarkColour);
    }

    private void DrawMarks(SKCanvas canvas,
                           ReadOnlySpan<double> values,
                           float top,
                           float height,
                           double scale,
                           SKColor colour)
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

            canvas.DrawRect(x, top + 1, RowBufferMarkWidth, height - 2, _paints.Fill);

            index = SortedSearch.FirstAtOrAfter(values, _viewStart + (Math.Floor(x) + RowBufferMarkWidth) / scale, index + 1);
        }
    }

    private int? MarkBandAt(Point position)
    {
        if (OperatorRowAt(position) is not { } found)
        {
            return null;
        }

        var height = MarkBandHeightOf(found.Row);

        return height > 0 && found.InRow >= OperatorRowHeight - height ? found.Row : null;
    }

    private void OpenNearestMark(int row, Point position)
    {
        if (RowBufferMarks is not { } marks)
        {
            return;
        }

        var scale = PixelsPerUnit((int)_overlay.ActualWidth);

        if (marks.GetNearestMark(_operatorRows[row].Operator, _axis, _viewStart + position.X / scale) is { } mark)
        {
            RowBufferOpenRequested?.Invoke(mark.Node, mark.Call, mark.IsBatch);
        }
    }

    private static void OnRowBuffersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FlameChartControl)d).Relayout();
}
