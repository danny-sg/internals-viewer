using System;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using SkiaSharp;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed partial class TraceTimelineControl
{
    private TracePosition? _trimStart;

    private TracePosition? _trimEnd;

    private TrimHandle _trimDrag;

    public event Action<TracePosition?, TracePosition?>? TrimChanged;

    private bool IsTrimmed => _trimStart is not null && _trimEnd is not null;

    private bool IsDraggingTrim => _trimDrag != TrimHandle.None;

    private double TrimFrom => IsTrimmed && GetAxisValue(_trimStart!.Value) is { } start ? Math.Max(start, FullStart) : FullStart;

    private double TrimTo => IsTrimmed && GetAxisValue(_trimEnd!.Value) is { } end ? Math.Min(end, FullEnd) : FullEnd;

    public void SetTrim(TracePosition? start, TracePosition? end)
    {
        var isSet = start is not null && end is not null && start < end;

        _trimStart = isSet ? start : null;

        _trimEnd = isSet ? end : null;

        ApplyTrim();

        if (isSet)
        {
            ZoomTo(TrimFrom, TrimTo);
        }
    }

    private double ClampToTrim(double value) => Math.Clamp(value, TrimFrom, Math.Max(TrimFrom, TrimTo));

    private void DragTrimTo(double value)
    {
        if (GetPosition(value) is not { } position)
        {
            return;
        }

        if (!IsTrimmed)
        {
            if (GetPlayheadPosition() is not { } playhead)
            {
                return;
            }

            _trimStart = playhead;

            _trimEnd = playhead;
        }

        if (_trimDrag == TrimHandle.Start)
        {
            _trimStart = position < _trimEnd!.Value ? position : _trimEnd;
        }
        else
        {
            _trimEnd = position > _trimStart!.Value ? position : _trimStart;
        }

        _canvas.Invalidate();
    }

    private void EndTrimDrag()
    {
        _trimDrag = TrimHandle.None;

        if (IsTrimmed && _trimStart == _trimEnd)
        {
            ClearTrim();

            return;
        }

        ApplyTrim();
    }

    private void ClearTrim()
    {
        _trimStart = null;

        _trimEnd = null;

        ApplyTrim();
    }

    private void ApplyTrim()
    {
        if (_playhead is { } playhead && (playhead < TrimFrom || playhead > TrimTo))
        {
            ShowPlayhead(ClampToTrim(playhead));

            PlayheadChanged?.Invoke();
        }

        CountSearchMatches();

        TrimChanged?.Invoke(_trimStart, _trimEnd);

        Redraw();
    }

    private void DrawTrim(SKCanvas canvas, int width, int height)
    {
        var scale = PixelsPerUnit(width);

        if (!IsTrimmed || GetTrimX(_trimStart!.Value, scale) is not { } start || GetTrimX(_trimEnd!.Value, scale) is not { } end)
        {
            return;
        }

        _paints.Fill.Color = _paints.Dim;

        if (start > 0)
        {
            canvas.DrawRect(0, OperatorsTop, start, height - OperatorsTop, _paints.Fill);
        }

        if (end < width)
        {
            canvas.DrawRect(end, OperatorsTop, width - end, height - OperatorsTop, _paints.Fill);
        }
    }

    private float? GetTrimX(TracePosition position, double scale)
        => GetAxisValue(position) is { } value ? (float)((value - _viewStart) * scale) : null;

    private double? GetAxisValue(TracePosition position)
    {
        if (_axis == TraceTimelineAxis.Position)
        {
            return TraceScale.GetValue(position);
        }

        if (_visible is not { } timeline)
        {
            return null;
        }

        foreach (var thread in timeline.Threads)
        {
            if (timeline.GetInnermostCall(thread.ThreadId, position) is not { } span)
            {
                continue;
            }

            var from = TraceScale.GetValue(span.From.Position);

            var to = TraceScale.GetValue(span.To.Position);

            var fraction = to > from ? Math.Clamp((TraceScale.GetValue(position) - from) / (to - from), 0, 1) : 0;

            return span.From.Instructions + (span.To.Instructions - span.From.Instructions) * fraction;
        }

        return null;
    }

    private enum TrimHandle
    {
        None,
        Start,
        End
    }
}
