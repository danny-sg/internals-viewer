using System;
using System.Collections.Generic;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed partial class TraceTimelineControl
{
    private const float PlayheadHalfWidth = 9f;

    private const float PlayheadBadgePadding = 4f;

    public TracePosition? GetPlayheadPosition() => _playhead is { } value ? GetPosition(value) : null;

    public TraceViewRange? GetViewRange()
        => _visible is null ? null : new TraceViewRange(_axis, _viewStart, _viewEnd, GetPosition(_viewStart), GetPosition(_viewEnd));

    private TracePosition? GetPosition(double value)
    {
        if (_axis == TraceTimelineAxis.Position)
        {
            return TraceScale.GetPosition(value);
        }

        if (_visible is not { } timeline)
        {
            return null;
        }

        foreach (var path in GetActivePaths())
        {
            for (var index = path.Count - 1; index >= 0; index--)
            {
                var hit = path[index];

                var row = timeline.Threads[hit.Lane].Rows[hit.Depth];

                var span = row.GetSpan(hit.Index);

                if (span.From.Instructions <= value && value <= span.To.Instructions)
                {
                    return GetPosition(row, hit.Index, value);
                }
            }
        }

        return null;
    }

    private void ShowPlayhead(double value)
    {
        _playhead = value;

        ClearPins();

        _stackFrame = null;

        if (_selected is not null)
        {
            Deselect();
        }
    }

    private double? GetSelectedStart()
        => _selected is { } selected && SpanOf(selected) is { } span ? TraceScale.GetValue(span.From, _axis) : null;

    private List<List<FlameHit>> GetActivePaths()
    {
        if (_playhead is { } value)
        {
            return GetPlayheadPaths(value);
        }

        var path = GetStackPath();

        return path.Count > 0 ? [path] : [];
    }

    private List<List<FlameHit>> GetPlayheadPaths(double value)
    {
        var paths = new List<List<FlameHit>>();

        if (_visible is not { } timeline)
        {
            return paths;
        }

        for (var lane = 0; lane < timeline.Threads.Count; lane++)
        {
            var rows = timeline.Threads[lane].Rows;

            var path = new List<FlameHit>();

            for (var depth = 0; depth < rows.Count; depth++)
            {
                var index = rows[depth].GetIndex(_axis, value, 0);

                if (index < 0)
                {
                    break;
                }

                path.Add(new FlameHit(lane, depth, index));
            }

            if (path.Count > 0)
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    private List<FlameHit> GetPlayheadFrames()
    {
        var frames = new List<FlameHit>();

        if (_playhead is not { } value || _visible is not { } timeline)
        {
            return frames;
        }

        for (var lane = 0; lane < timeline.Threads.Count; lane++)
        {
            var rows = timeline.Threads[lane].Rows;

            FlameHit? deepest = null;

            for (var depth = 0; depth < rows.Count; depth++)
            {
                var index = rows[depth].GetIndex(_axis, value, 0);

                if (index < 0)
                {
                    break;
                }

                deepest = new FlameHit(lane, depth, index);
            }

            if (deepest is { } frame)
            {
                frames.Add(frame);
            }
        }

        return frames;
    }

    private void DrawPlayhead(SKCanvas canvas, int width, int height)
    {
        if (_playhead is not { } value || _visible is not { } timeline)
        {
            return;
        }

        var scale = PixelsPerUnit(width);

        var x = (float)((value - _viewStart) * scale);

        if (x < 0 || x > width)
        {
            return;
        }

        canvas.DrawLine(x, OperatorsTop, x, height, _paints.Playhead);

        DrawBandMarker(canvas, timeline, x, width, height, scale);

        DrawPlayheadHandle(canvas, x, value, width);
    }

    private void DrawPlayheadHandle(SKCanvas canvas, float x, double value, int width)
    {
        canvas.Save();

        canvas.Translate(x, RulerTop);

        canvas.DrawPath(_playheadTriangle, _paints.PlayheadFill);

        canvas.Restore();

        var text = PlayheadLabel(value);

        var badgeWidth = _paints.Font.MeasureText(text) + PlayheadBadgePadding * 2;

        var badgeHeight = RulerHeight - 2;

        var left = Math.Clamp(x - badgeWidth / 2, 0, Math.Max(0, width - badgeWidth));

        canvas.DrawRoundRect(new SKRect(left, RulerTop, left + badgeWidth, RulerTop + badgeHeight), 2, 2, _paints.PlayheadFill);

        canvas.DrawText(text,
                        left + PlayheadBadgePadding,
                        Baseline(RulerTop, badgeHeight),
                        SKTextAlign.Left,
                        _paints.Font,
                        _paints.PlayheadText);
    }

    private string PlayheadLabel(double value)
        => _axis == TraceTimelineAxis.Position
            ? TracePositionText.GetHexText(TraceScale.GetPosition(value))
            : $"{value - OriginOf(_axis)}";

    private static SKPath PlayheadTriangle()
    {
        using var builder = new SKPathBuilder();

        builder.MoveTo(0, RulerHeight + PlayheadStripHeight);

        builder.LineTo(-PlayheadHalfWidth, RulerHeight);

        builder.LineTo(PlayheadHalfWidth, RulerHeight);

        builder.Close();

        return builder.Detach();
    }
}
