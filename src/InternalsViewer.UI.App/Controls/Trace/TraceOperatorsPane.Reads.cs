using System;
using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.UI.App.Controls.Timeline;
using SkiaSharp;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Trace;

internal sealed partial class TraceOperatorsPane
{
    private const float ReadMarkerWidth = 1f;

    private const float SparseReadMarkerWidth = 4f;

    private const int SparseReadCount = 25;

    private const byte ReadDurationAlpha = 96;

    private static readonly SKColor ReadHoverColour = new(255, 255, 255, 200);

    public TraceReads? Reads { get; set; }

    public bool ShowReads { get; set; } = true;

    public TraceRead? GetReadAt(Point position)
    {
        if (GetOperatorAt(position) is not { } hit
            || GetReads(Rows[hit.Row], Rows[hit.Row].Lifetimes[hit.Track]) is not { } reads
            || GetTrackRect(hit.Row, hit.Track, Host.Width, Host.Scale) is not { } bar)
        {
            return null;
        }

        var height = GetReturnStripHeight(bar);

        if (height <= 0 || position.Y < bar.Bottom - height || position.Y >= bar.Bottom)
        {
            return null;
        }

        var scale = Host.Scale;

        return reads.GetNearest(Axis, Host.ViewStart + position.X / scale, TraceTimelineControl.HitTolerance / scale);
    }

    private TraceReadIndex? GetReads(TraceOperatorRow row, TraceOperatorLifetime lifetime)
        => ShowReads ? Reads?.GetReads(row.Operator, lifetime.Thread) : null;

    private bool HasReadStrip(TraceOperatorRow row, TraceOperatorLifetime lifetime) => GetReads(row, lifetime) is not null;

    private void DrawReadStrip(SKCanvas canvas, TraceOperatorRow row, TraceOperatorLifetime lifetime, SKRect bar, SKColor colour, double scale)
    {
        var height = GetReturnStripHeight(bar);

        if (height <= 0 || GetReads(row, lifetime) is not { } reads)
        {
            return;
        }

        var top = bar.Bottom - height;

        canvas.Save();

        canvas.ClipRoundRect(Shape, antialias: true);

        Paints.Fill.Color = TimelineColours.Scale(colour, ReturnStripShade);

        canvas.DrawRect(bar.Left, top, bar.Width, height, Paints.Fill);

        DrawReads(canvas, reads, top + 1, height - 2, colour, scale);

        canvas.Restore();
    }

    private void DrawReads(SKCanvas canvas, TraceReadIndex reads, float top, float height, SKColor colour, double scale)
    {
        var starts = reads.GetStarts(Axis);

        var markerWidth = GetReadMarkerWidth(reads);

        var nextPagePixel = float.MinValue;

        for (var index = reads.GetFirstEndingFrom(Axis, Host.ViewStart); index < starts.Length && starts[index] <= Host.ViewEnd; index++)
        {
            var read = reads.GetRead(Axis, index);

            var span = read.Of(Axis);

            var left = (float)((span.Start - Host.ViewStart) * scale);

            var right = Math.Max((float)((span.End - Host.ViewStart) * scale), left + markerWidth);

            var isThinned = read.Type == TraceReadType.Page && (span.End - span.Start) * scale <= markerWidth;

            if (isThinned && left < nextPagePixel)
            {
                continue;
            }

            if (right - left > markerWidth)
            {
                Paints.Fill.Color = colour.WithAlpha(ReadDurationAlpha);

                canvas.DrawRect(left, top, right - left, height, Paints.Fill);
            }

            Paints.Fill.Color = colour;

            canvas.DrawRect(right - markerWidth, top, markerWidth, height, Paints.Fill);

            if (isThinned)
            {
                nextPagePixel = MathF.Floor(right) + 1;
            }
        }
    }

    private void DrawReadHover(SKCanvas canvas, double scale)
    {
        if (Hover.Read is not { Operator: { } operatorEvent } read
            || GetTrack(operatorEvent, read.Thread) is not { } track
            || GetReads(Rows[track.Row], Rows[track.Row].Lifetimes[track.Track]) is not { } reads
            || GetTrackRect(track.Row, track.Track, Host.Width, scale) is not { } bar)
        {
            return;
        }

        var height = GetReturnStripHeight(bar);

        var span = read.Of(Axis);

        var left = (float)((span.Start - Host.ViewStart) * scale);

        var right = Math.Max((float)((span.End - Host.ViewStart) * scale), left + GetReadMarkerWidth(reads));

        Paints.Outline.Color = ReadHoverColour;

        Paints.Outline.StrokeWidth = 1f;

        canvas.DrawRect(new SKRect(left, bar.Bottom - height, right, bar.Bottom), Paints.Outline);
    }

    private string GetReadToolTip(TraceRead read)
    {
        var origin = Host.GetOrigin(TraceTimelineAxis.Position);

        var lines = new List<string>
        {
            read.Type == TraceReadType.ReadAhead
                ? $"Read Ahead {read.PageCount:N0} {(read.PageCount == 1 ? "Page" : "Pages")} From {read.Page}"
                : $"Page {read.Page}",
            read.Node.Symbol,
            $"Thread {read.Thread}  ·  Call {read.Call + 1:N0}",
            $"Position {Math.Floor(read.Position.Start - origin):N0} to {Math.Floor(read.Position.End - origin):N0}"
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static float GetReadMarkerWidth(TraceReadIndex reads) => reads.Count < SparseReadCount ? SparseReadMarkerWidth : ReadMarkerWidth;
}
