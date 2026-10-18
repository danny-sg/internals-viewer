using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;
using InternalsViewer.UI.App.Controls.Timeline;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Trace;

public sealed partial class TraceTimelineControl
{
    private const float ReturnStripHeight = 7f;

    private const float ReturnStripShare = 0.35f;

    private const float MinimumReturnStripHeight = 2f;

    private const float ReturnStripShade = 0.5f;

    private const float ReturnStripHoverShade = 0.7f;

    private const float MinimumCallDurationWidth = 1f;

    private const byte CallDurationAlpha = 0x60;

    private const float CallDurationGap = 1f;

    private const string ReturnStripTitle = "GetRow Returns";

    private const string BatchReturnStripTitle = "GetBatch Returns";

    private static readonly ReturnStyle[] ReturnStyles =
    [
        new(RowReturnType.NotPassedUp, new SKColor(0x8A, 0x8A, 0x8A), "Not Passed Up"),
        new(RowReturnType.Rejected, new SKColor(0xF0, 0x48, 0x48), "Rejected"),
        new(RowReturnType.PassedUp, null, "Passed Up"),
        new(RowReturnType.FakeRow, new SKColor(0xFF, 0xC8, 0x57), "Fake Row"),
        new(RowReturnType.Other, new SKColor(0xC0, 0x84, 0xFC), "Other Status"),
        new(RowReturnType.End, new SKColor(0xF0, 0xF0, 0xF0), "End")
    ];

    public static readonly DependencyProperty RowReturnMarksProperty =
        DependencyProperty.Register(nameof(RowReturnMarks),
                                    typeof(TraceRowReturnMarks),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnRowBuffersChanged));

    public TraceRowReturnMarks? RowReturnMarks
    {
        get => (TraceRowReturnMarks?)GetValue(RowReturnMarksProperty);
        set => SetValue(RowReturnMarksProperty, value);
    }

    public static readonly DependencyProperty ShowIteratorCallsProperty =
        DependencyProperty.Register(nameof(ShowIteratorCalls),
                                    typeof(bool),
                                    typeof(TraceTimelineControl),
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

        DrawCallDurations(canvas,
                          marks.GetCallStarts(row.Operator, lifetime.Thread, _axis),
                          marks.GetCallEnds(row.Operator, lifetime.Thread, _axis),
                          bar.Top,
                          height,
                          colour.WithAlpha(CallDurationAlpha),
                          scale);

        foreach (var style in ReturnStyles)
        {
            var returns = marks.GetMarks(row.Operator, lifetime.Thread, _axis, style.Type);

            DrawMarks(canvas, returns, bar.Top, height, scale, style.Colour ?? colour);
        }

        canvas.Restore();
    }

    private void DrawCallDurations(SKCanvas canvas,
                                   ReadOnlySpan<double> starts,
                                   ReadOnlySpan<double> ends,
                                   float top,
                                   float height,
                                   SKColor colour,
                                   double scale)
    {
        _paints.Fill.Color = colour;

        var index = SortedSearch.FirstAtOrAfter(ends, _viewStart);

        while (index < ends.Length && starts[index] <= _viewEnd)
        {
            var left = (float)((starts[index] - _viewStart) * scale);

            var right = (float)((ends[index] - _viewStart) * scale);

            if (right - left > MinimumCallDurationWidth)
            {
                canvas.DrawRect(left, top, right - left - CallDurationGap, height, _paints.Fill);

                index++;
            }
            else
            {
                index = SortedSearch.FirstAtOrAfter(ends, _viewStart + (Math.Floor(right) + 1) / scale, index + 1);
            }
        }
    }

    private FlameHit? GetCallDuration(OperatorHit hit, Point position)
    {
        if (RowReturnMarks is not { } marks || _visible is not { } timeline)
        {
            return null;
        }

        var scale = PixelsPerUnit((int)_overlay.ActualWidth);

        var lifetime = _operatorRows[hit.Row].Lifetimes[hit.Track];

        if (marks.GetCall(_operatorRows[hit.Row].Operator, lifetime.Thread, _axis, _viewStart + position.X / scale) is not { } call
            || (call.End - call.Start) * scale <= MinimumCallDurationWidth)
        {
            return null;
        }

        return FindCall(timeline, call.Node, call.Call);
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

        List<string> lines = [marks.IsBatch(operatorEvent) ? BatchReturnStripTitle : ReturnStripTitle];

        foreach (var style in ReturnStyles.OrderBy(s => s.Type))
        {
            if (marks.GetCount(operatorEvent, thread, style.Type) is var count and > 0)
            {
                lines.Add($"{style.Label}: {count:N0}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void ClickReturnStrip(OperatorHit hit, Point position)
    {
        if (GetCallDuration(hit, position) is not { } duration)
        {
            OpenNearestReturn(hit, position);

            return;
        }

        Click(duration);

        if (_selected is { } selected)
        {
            BringIntoView(selected);
        }
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
