using System;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using SkiaSharp;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed partial class TraceTimelineControl
{
    private const float TrimHandleWidth = 7f;

    private const float TrimHandleHeight = 8f;

    private const float TrimHandleGap = PlayheadHalfWidth + 4f;

    private const float TrimHitWidth = 6f;

    private static readonly SKColor TrimHandleColour = new(128, 128, 128);

    private readonly SKPath _trimStartHandle = CreateTrimHandle(isStart: true);

    private readonly SKPath _trimEndHandle = CreateTrimHandle(isStart: false);

    private TracePosition? _trimStart;

    private TracePosition? _trimEnd;

    private TrimHandle _trimDrag;

    public event Action<TracePosition?, TracePosition?>? TrimChanged;

    private bool IsTrimmed => _trimStart is not null && _trimEnd is not null;

    private bool IsDraggingTrim => _trimDrag != TrimHandle.None;

    private double TrimFrom => IsTrimmed && GetAxisValue(_trimStart!.Value) is { } start ? Math.Max(start, FullStart) : FullStart;

    private double TrimTo => IsTrimmed && GetAxisValue(_trimEnd!.Value) is { } end ? Math.Min(end, FullEnd) : FullEnd;

    private double ClampToTrim(double value) => Math.Clamp(value, TrimFrom, Math.Max(TrimFrom, TrimTo));

    private TrimHandle GetTrimHandleAt(Point position)
    {
        if (!IsInPlayheadStrip(position) || position.Y < OperatorsTop - TrimHandleHeight - TrimHitWidth)
        {
            return TrimHandle.None;
        }

        var (startX, endX) = GetTrimHandleXs();

        var toStart = startX is { } start ? Math.Abs(position.X - start) : double.MaxValue;

        var toEnd = endX is { } end ? Math.Abs(position.X - end) : double.MaxValue;

        if (toStart <= TrimHitWidth && toStart <= toEnd)
        {
            return TrimHandle.Start;
        }

        return toEnd <= TrimHitWidth ? TrimHandle.End : TrimHandle.None;
    }

    private void DragTrim(double x)
    {
        var scale = PixelsPerUnit((int)_overlay.ActualWidth);

        if (GetPosition(Math.Clamp(_viewStart + x / scale, _viewStart, _viewEnd)) is not { } position)
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
        var (startX, endX) = GetTrimHandleXs();

        if (IsTrimmed && startX is { } start && endX is { } end)
        {
            _paints.Fill.Color = _paints.Dim;

            if (start > 0)
            {
                canvas.DrawRect(0, PlayheadStripTop, start, height - PlayheadStripTop, _paints.Fill);
            }

            if (end < width)
            {
                canvas.DrawRect(end, PlayheadStripTop, width - end, height - PlayheadStripTop, _paints.Fill);
            }
        }

        _paints.Fill.Color = TrimHandleColour;

        DrawTrimHandle(canvas, _trimStartHandle, startX);

        DrawTrimHandle(canvas, _trimEndHandle, endX);
    }

    private void DrawTrimHandle(SKCanvas canvas, SKPath handle, float? x)
    {
        if (x is not { } left)
        {
            return;
        }

        canvas.Save();

        canvas.Translate(left, OperatorsTop - TrimHandleHeight);

        canvas.DrawPath(handle, _paints.Fill);

        canvas.Restore();
    }

    private (float? Start, float? End) GetTrimHandleXs()
    {
        var scale = PixelsPerUnit((int)_overlay.ActualWidth);

        if (IsTrimmed)
        {
            return (GetTrimX(_trimStart!.Value, scale), GetTrimX(_trimEnd!.Value, scale));
        }

        if (_playhead is not { } playhead)
        {
            return (null, null);
        }

        var x = (float)((playhead - _viewStart) * scale);

        return (x - TrimHandleGap, x + TrimHandleGap);
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

    private static SKPath CreateTrimHandle(bool isStart)
    {
        using var builder = new SKPathBuilder();

        var half = TrimHandleWidth / 2;

        builder.MoveTo(-half, 0);

        builder.LineTo(half, 0);

        builder.LineTo(isStart ? -half : half, TrimHandleHeight);

        builder.Close();

        return builder.Detach();
    }

    private enum TrimHandle
    {
        None,
        Start,
        End
    }
}
