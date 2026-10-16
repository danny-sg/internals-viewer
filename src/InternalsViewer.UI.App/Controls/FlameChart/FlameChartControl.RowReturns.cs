using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.UI.App.Controls.Timeline;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.FlameChart;

public sealed partial class FlameChartControl
{
    private const float ReturnStripHeight = 6f;

    private const float ReturnStripShare = 0.35f;

    private const float MinimumReturnStripHeight = 2f;

    private const float ReturnStripShade = 0.5f;

    private const float ReturnStripHoverShade = 0.7f;

    private const string ReturnStripTitle = "GetRow Returns";

    private static readonly ReturnStyle[] ReturnStyles =
    [
        new(RowReturnType.NotPassedUp, new SKColor(0x8A, 0x8A, 0x8A), "Not Passed Up"),
        new(RowReturnType.PassedUp, null, "Passed Up"),
        new(RowReturnType.FakeRow, new SKColor(0xFF, 0xC8, 0x57), "Fake Row"),
        new(RowReturnType.Other, new SKColor(0xFF, 0x6B, 0x6B), "Other Status"),
        new(RowReturnType.End, new SKColor(0xF0, 0xF0, 0xF0), "End")
    ];

    public static readonly DependencyProperty RowReturnMarksProperty =
        DependencyProperty.Register(nameof(RowReturnMarks),
                                    typeof(TraceRowReturnMarks),
                                    typeof(FlameChartControl),
                                    new PropertyMetadata(null, OnRowBuffersChanged));

    public TraceRowReturnMarks? RowReturnMarks
    {
        get => (TraceRowReturnMarks?)GetValue(RowReturnMarksProperty);
        set => SetValue(RowReturnMarksProperty, value);
    }

    public static readonly DependencyProperty ShowIteratorCallsProperty =
        DependencyProperty.Register(nameof(ShowIteratorCalls),
                                    typeof(bool),
                                    typeof(FlameChartControl),
                                    new PropertyMetadata(true, OnRowBuffersChanged));

    public bool ShowIteratorCalls
    {
        get => (bool)GetValue(ShowIteratorCallsProperty);
        set => SetValue(ShowIteratorCallsProperty, value);
    }

    private OperatorHit? _hoverReturnStrip;

    private static float ReturnStripHeightOf(SKRect bar)
    {
        var height = Math.Min(ReturnStripHeight, bar.Height * ReturnStripShare);

        return height >= MinimumReturnStripHeight ? height : 0;
    }

    private void DrawReturnStrip(SKCanvas canvas,
                                 OperatorRow row,
                                 TraceOperatorLifetime lifetime,
                                 SKRect bar,
                                 SKColor colour,
                                 double scale)
    {
        var height = ReturnStripHeightOf(bar);

        if (height <= 0 || !ShowIteratorCalls || RowReturnMarks is not { } marks || !marks.HasMarks(row.Operator, lifetime.Thread))
        {
            return;
        }

        var isHovered = _hoverReturnStrip is { } hover && ReferenceEquals(_operatorRows[hover.Row].Lifetimes[hover.Track], lifetime);

        canvas.Save();

        canvas.ClipRoundRect(_operatorShape, antialias: true);

        _paints.Fill.Color = TimelineColours.Scale(colour, isHovered ? ReturnStripHoverShade : ReturnStripShade);

        canvas.DrawRect(bar.Left, bar.Top, bar.Width, height, _paints.Fill);

        foreach (var style in ReturnStyles)
        {
            var returns = marks.GetMarks(row.Operator, lifetime.Thread, _axis, style.Type);

            DrawMarks(canvas, returns, bar.Top, height, scale, style.Colour ?? colour);
        }

        canvas.Restore();
    }

    private OperatorHit? ReturnStripAt(Point position)
    {
        if (!ShowIteratorCalls || RowReturnMarks is not { } marks || OperatorAt(position) is not { } hit)
        {
            return null;
        }

        var lifetime = _operatorRows[hit.Row].Lifetimes[hit.Track];

        if (!marks.HasMarks(_operatorRows[hit.Row].Operator, lifetime.Thread)
            || TrackRect(hit.Row, hit.Track, (int)_overlay.ActualWidth, PixelsPerUnit((int)_overlay.ActualWidth)) is not { } bar)
        {
            return null;
        }

        var height = ReturnStripHeightOf(bar);

        return height > 0 && position.Y >= bar.Top && position.Y < bar.Top + height ? hit : null;
    }

    private string ReturnStripToolTipOf(OperatorHit hit)
    {
        if (RowReturnMarks is not { } marks)
        {
            return ReturnStripTitle;
        }

        var operatorEvent = _operatorRows[hit.Row].Operator;

        var thread = _operatorRows[hit.Row].Lifetimes[hit.Track].Thread;

        List<string> lines = [ReturnStripTitle];

        foreach (var style in ReturnStyles.OrderBy(s => s.Type))
        {
            if (marks.GetCount(operatorEvent, thread, style.Type) is var count and > 0)
            {
                lines.Add($"{style.Label}: {count:N0}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void OpenNearestReturn(OperatorHit hit, Point position)
    {
        if (RowReturnMarks is not { } marks)
        {
            return;
        }

        var scale = PixelsPerUnit((int)_overlay.ActualWidth);

        var operatorEvent = _operatorRows[hit.Row].Operator;

        var thread = _operatorRows[hit.Row].Lifetimes[hit.Track].Thread;

        if (marks.GetNearestMark(operatorEvent, thread, _axis, _viewStart + position.X / scale) is { } mark)
        {
            CallTreeOpenRequested?.Invoke(mark.Node, mark.Call);
        }
    }

    private readonly record struct ReturnStyle(RowReturnType Type, SKColor? Colour, string Label);
}
