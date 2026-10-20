using System;
using System.Collections.Generic;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using SkiaSharp;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Trace;

internal sealed partial class TraceOperatorsPane
{
    private const float MarkWidth = 1f;

    private const float SparseMarkWidth = 4f;

    private const int SparseMarkCount = 25;

    private const byte MarkDurationAlpha = 96;

    private static readonly SKColor MarkHoverColour = new(255, 255, 255, 200);

    private (float Top, float Height)? GetStrip(SKRect bar, int level)
    {
        var height = GetReturnStripHeight(bar);

        return height > 0 ? (bar.Bottom - height * (level + 1), height) : null;
    }

    private (TraceOperatorHit Hit, TraceMarkIndex<T> Marks)? GetStripAt<T>(
        Point position,
        Func<TraceOperatorRow, TraceOperatorLifetime, TraceMarkIndex<T>?> getMarks,
        Func<TraceOperatorRow, TraceOperatorLifetime, int> getLevel)
        where T : ITraceMark
    {
        if (GetOperatorAt(position) is not { } hit)
        {
            return null;
        }

        var row = Rows[hit.Row];

        var lifetime = row.Lifetimes[hit.Track];

        if (getMarks(row, lifetime) is not { } marks
            || GetTrackRect(hit.Row, hit.Track, Host.Width, Host.Scale) is not { } bar
            || GetStrip(bar, getLevel(row, lifetime)) is not { } strip
            || position.Y < strip.Top
            || position.Y >= strip.Top + strip.Height)
        {
            return null;
        }

        return (hit, marks);
    }

    private T? GetNearestMark<T>(TraceMarkIndex<T> marks, Point position) where T : class, ITraceMark
    {
        var scale = Host.Scale;

        return marks.GetNearest(Axis, Host.ViewStart + position.X / scale, TraceTimelineControl.HitTolerance / scale);
    }

    private void DrawStrip<T>(SKCanvas canvas,
                              SKRect bar,
                              (float Top, float Height) strip,
                              SKColor background,
                              TraceMarkIndex<T> marks,
                              IReadOnlyList<Func<T, SKColor?>> passes,
                              Func<T, bool> isThinnable,
                              double scale)
        where T : ITraceMark
    {
        canvas.Save();

        canvas.ClipRoundRect(Shape, antialias: true);

        Paints.Fill.Color = background;

        canvas.DrawRect(bar.Left, strip.Top, bar.Width, strip.Height, Paints.Fill);

        foreach (var getColour in passes)
        {
            DrawMarks(canvas, marks, strip.Top + 1, strip.Height - 2, getColour, isThinnable, scale);
        }

        canvas.Restore();
    }

    private void DrawMarks<T>(SKCanvas canvas,
                              TraceMarkIndex<T> marks,
                              float top,
                              float height,
                              Func<T, SKColor?> getColour,
                              Func<T, bool> isThinnable,
                              double scale)
        where T : ITraceMark
    {
        var starts = marks.GetStarts(Axis);

        var markWidth = GetMarkWidth(marks.Count);

        var nextPixel = float.MinValue;

        for (var index = marks.GetFirstEndingFrom(Axis, Host.ViewStart); index < starts.Length && starts[index] <= Host.ViewEnd; index++)
        {
            var mark = marks.Get(Axis, index);

            if (getColour(mark) is not { } colour)
            {
                continue;
            }

            var span = mark.Of(Axis);

            var left = (float)((span.Start - Host.ViewStart) * scale);

            var right = Math.Max((float)((span.End - Host.ViewStart) * scale), left + markWidth);

            var isThinned = isThinnable(mark) && (span.End - span.Start) * scale <= markWidth;

            if (isThinned && left < nextPixel)
            {
                continue;
            }

            if (right - left > markWidth)
            {
                Paints.Fill.Color = colour.WithAlpha(MarkDurationAlpha);

                canvas.DrawRect(left, top, right - left, height, Paints.Fill);
            }

            Paints.Fill.Color = colour;

            canvas.DrawRect(right - markWidth, top, markWidth, height, Paints.Fill);

            if (isThinned)
            {
                nextPixel = MathF.Floor(right) + 1;
            }
        }
    }

    private void DrawMarkHover<T>(SKCanvas canvas,
                                  T? mark,
                                  ExecutionOperatorEvent? operatorEvent,
                                  uint thread,
                                  Func<TraceOperatorRow, TraceOperatorLifetime, TraceMarkIndex<T>?> getMarks,
                                  Func<TraceOperatorRow, TraceOperatorLifetime, int> getLevel,
                                  double scale)
        where T : class, ITraceMark
    {
        if (mark is null || operatorEvent is null || GetTrack(operatorEvent, thread) is not { } track)
        {
            return;
        }

        var row = Rows[track.Row];

        var lifetime = row.Lifetimes[track.Track];

        if (getMarks(row, lifetime) is not { } marks
            || GetTrackRect(track.Row, track.Track, Host.Width, scale) is not { } bar
            || GetStrip(bar, getLevel(row, lifetime)) is not { } strip)
        {
            return;
        }

        var span = mark.Of(Axis);

        var left = (float)((span.Start - Host.ViewStart) * scale);

        var right = Math.Max((float)((span.End - Host.ViewStart) * scale), left + GetMarkWidth(marks.Count));

        Paints.Outline.Color = MarkHoverColour;

        Paints.Outline.StrokeWidth = 1f;

        canvas.DrawRect(new SKRect(left, strip.Top, right, strip.Top + strip.Height), Paints.Outline);
    }

    private string GetMarkPosition(TraceSpan position)
    {
        var origin = Host.GetOrigin(TraceTimelineAxis.Position);

        return $"Position {Math.Floor(position.Start - origin):N0} to {Math.Floor(position.End - origin):N0}";
    }

    private static float GetMarkWidth(int count) => count < SparseMarkCount ? SparseMarkWidth : MarkWidth;
}
