using System;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Trace;

public sealed partial class TraceTimelineControl
{
    private const float PlayheadHalfWidth = 9f;

    private const float PlayheadBadgePadding = 4f;

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
    {
        var relative = value - OriginOf(_axis);

        if (_axis != TraceTimelineAxis.Position || _timeline is not { } timeline)
        {
            return $"{relative}";
        }

        var sequence = Math.Floor(relative);

        return $"{sequence}:{Math.Round((relative - sequence) / timeline.Scale.GetStep(_axis))}";
    }

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
