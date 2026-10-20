using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;
using InternalsViewer.UI.App.Controls.Timeline;
using InternalsViewer.UI.App.Controls.Trace.Enums;
using SkiaSharp;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Trace;

internal sealed partial class TraceOperatorsPane
{
    private const float StripHeight = 9f;

    private const float StripShare = 0.35f;

    private const float MinimumStripHeight = 2f;

    private const float StripShade = 0.5f;

    private const float StripHoverShade = 0.7f;

    private const float BackgroundHoverAlpha = 1.5f;

    private const float SparseTickWidth = 4f;

    private const int SparseTickCount = 25;

    private const byte TickBodyAlpha = 0x60;

    private const float TickBodyGap = 1f;

    private const float MinimumDurationWidth = 1f;

    private const float MinimumCallLinkWidth = 3f;

    private static readonly SKColor TickHoverColour = new(255, 255, 255, 200);

    private TraceTickTrack[] VisibleTracks { get; set; } = [];

    public void SetTracks(IReadOnlyList<TraceTickTrack>? tracks, IReadOnlySet<string>? hidden)
    {
        VisibleTracks = [.. (tracks ?? []).Where(t => hidden?.Contains(t.Name) != true).OrderBy(t => t.Location).ThenBy(t => t.Ordinal)];

        Hover = TraceOperatorsHover.None;
    }

    public TraceTick? GetTickAt(Point position) => GetTickHit(position)?.Tick;

    private static float GetStripHeight(SKRect bar)
    {
        var height = Math.Min(StripHeight, bar.Height * StripShare);

        return height >= MinimumStripHeight ? height : 0;
    }

    private static SKRect GetLabelRect(SKRect bar, List<TickStrip> strips)
    {
        var top = bar.Top;

        var bottom = bar.Bottom;

        foreach (var strip in strips)
        {
            if (strip.Track.Location == TraceTickLocation.Top)
            {
                top = Math.Max(top, strip.Top + strip.Height);
            }
            else
            {
                bottom = Math.Min(bottom, strip.Top);
            }
        }

        return bar with { Top = top, Bottom = bottom };
    }

    private static string GetValueLabel(TraceTickTrack track, byte value)
        => track.Values.FirstOrDefault(v => v.Value == value).Label ?? value.ToString();

    private static SKColor GetStripBackground(TraceTickTrack track, SKColor colour, bool isHovered)
        => track.Background is { } background
            ? isHovered ? background.WithAlpha((byte)Math.Min(background.Alpha * BackgroundHoverAlpha, byte.MaxValue)) : background
            : TimelineColours.Scale(colour, isHovered ? StripHoverShade : StripShade);

    private static float GetTickWidth(TraceTickTrack track, int count)
        => (count < SparseTickCount ? Math.Max(SparseTickWidth, track.TickWidth) : track.TickWidth) * track.WidthFactor;

    private static float GetFirstPixelCentre(float left) => MathF.Ceiling(left - 0.5f) + 0.5f;

    private static float GetLastPixelCentre(float right) => MathF.Ceiling(right - 0.5f) - 0.5f;

    private (float Left, float Right) GetTickEdges(TraceSpan span, float width, double scale)
    {
        var left = (float)((span.Start - Host.ViewStart) * scale);

        return (left, Math.Max((float)((span.End - Host.ViewStart) * scale), left + width));
    }

    private List<TickStrip> GetStrips(int row, int track, SKRect bar)
    {
        var strips = new List<TickStrip>();

        var baseHeight = GetStripHeight(bar);

        if (baseHeight <= 0)
        {
            return strips;
        }

        var operatorEvent = Rows[row].Operator;

        var thread = Rows[row].Lifetimes[track].Thread;

        var top = 0f;

        var bottom = 0f;

        foreach (var tickTrack in VisibleTracks)
        {
            if (tickTrack.Series.GetTicks(operatorEvent, thread) is not { Count: > 0 } ticks)
            {
                continue;
            }

            var height = baseHeight * tickTrack.HeightFactor;

            if (top + bottom + height > bar.Height)
            {
                break;
            }

            var y = tickTrack.Location == TraceTickLocation.Top ? bar.Top + top : bar.Bottom - bottom - height;

            if (tickTrack.Location == TraceTickLocation.Top)
            {
                top += height;
            }
            else
            {
                bottom += height;
            }

            strips.Add(new TickStrip(tickTrack, ticks, y, height));
        }

        return strips;
    }

    private SKRect DrawStrips(SKCanvas canvas, int row, int track, SKRect bar, SKColor colour, double scale)
    {
        var strips = GetStrips(row, track, bar);

        if (strips.Count == 0)
        {
            return bar;
        }

        canvas.Save();

        canvas.ClipRoundRect(Shape, antialias: true);

        foreach (var strip in strips)
        {
            var isHovered = Hover.Tick is { } hover
                            && hover.Row == row
                            && hover.Track == track
                            && ReferenceEquals(hover.TickTrack, strip.Track);

            Paints.Fill.Color = GetStripBackground(strip.Track, colour, isHovered);

            canvas.DrawRect(bar.Left, strip.Top, bar.Width, strip.Height, Paints.Fill);

            var width = GetTickWidth(strip.Track, strip.Ticks.Count);

            foreach (var value in strip.Track.Values)
            {
                DrawTicks(canvas, strip, value.Value, colour, width, scale);
            }
        }

        canvas.Restore();

        return GetLabelRect(bar, strips);
    }

    private void DrawStripShades(SKCanvas canvas, int row, int track, SKRect rect, SKColor colour)
    {
        foreach (var strip in GetStrips(row, track, rect))
        {
            Paints.Fill.Color = GetStripBackground(strip.Track, colour, false);

            canvas.DrawRect(rect.Left, strip.Top, rect.Width, strip.Height, Paints.Fill);
        }
    }

    private void DrawTicks(SKCanvas canvas, TickStrip strip, byte value, SKColor operatorColour, float width, double scale)
    {
        var ticks = strip.Ticks;

        var starts = ticks.GetStarts(Axis);

        var top = strip.Top + 1;

        var height = strip.Height - 2;

        var nextPixel = float.MinValue;

        for (var index = ticks.GetFirstEndingFrom(Axis, Host.ViewStart); index < starts.Length && starts[index] <= Host.ViewEnd; index++)
        {
            var tick = ticks.Get(Axis, index);

            if (tick.Value != value)
            {
                continue;
            }

            var span = tick.Of(Axis);

            var (left, right) = GetTickEdges(span, width, scale);

            var spanPixels = (span.End - span.Start) * scale;

            var isThinned = spanPixels <= width;

            if (isThinned && left < nextPixel)
            {
                continue;
            }

            var colour = strip.Track.GetColour(tick) ?? operatorColour;

            if (spanPixels > MinimumDurationWidth && right - left > width)
            {
                Paints.Fill.Color = colour.WithAlpha(TickBodyAlpha);

                canvas.DrawRect(left, top, Math.Max(right - left - TickBodyGap, 0), height, Paints.Fill);
            }

            Paints.Fill.Color = colour;

            canvas.DrawRect(right - width, top, width, height, Paints.Fill);

            if (isThinned)
            {
                nextPixel = MathF.Floor(right) + 1;
            }
        }
    }

    private TraceTickHit? GetTickHit(Point position)
    {
        if (GetOperatorAt(position) is not { } hit || GetTrackRect(hit.Row, hit.Track, Host.Width, Host.Scale) is not { } bar)
        {
            return null;
        }

        foreach (var strip in GetStrips(hit.Row, hit.Track, bar))
        {
            if (position.Y < strip.Top || position.Y >= strip.Top + strip.Height)
            {
                continue;
            }

            var scale = Host.Scale;

            var tick = strip.Ticks.GetNearest(Axis, Host.ViewStart + position.X / scale, TraceTimelineControl.HitTolerance / scale);

            return new TraceTickHit(hit.Row, hit.Track, strip.Track, tick);
        }

        return null;
    }

    private void DrawTickHover(SKCanvas canvas, double scale)
    {
        if (Hover.Tick is not { Tick: { } tick } hover
            || hover.Row >= Rows.Length
            || GetTrackRect(hover.Row, hover.Track, Host.Width, scale) is not { } bar)
        {
            return;
        }

        foreach (var strip in GetStrips(hover.Row, hover.Track, bar))
        {
            if (!ReferenceEquals(strip.Track, hover.TickTrack))
            {
                continue;
            }

            var (left, right) = GetTickEdges(tick.Of(Axis), GetTickWidth(strip.Track, strip.Ticks.Count), scale);

            Paints.Outline.Color = TickHoverColour;

            Paints.Outline.StrokeWidth = 1f;

            canvas.DrawRect(new SKRect(left, strip.Top, right, strip.Top + strip.Height), Paints.Outline);
        }
    }

    private string GetTickToolTip(TraceTickHit hit)
    {
        var row = Rows[hit.Row];

        var track = hit.TickTrack;

        var lines = new List<string> { track.GetTitle?.Invoke(row.Operator) ?? track.Name };

        if (hit.Tick is not { } tick)
        {
            var ticks = track.Series.GetTicks(row.Operator, row.Lifetimes[hit.Track].Thread)?.All ?? [];

            foreach (var value in track.Values.OrderBy(v => v.Value))
            {
                if (ticks.Count(t => t.Value == value.Value) is var count and > 0)
                {
                    lines.Add($"{value.Label}: {count:N0}");
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        if (track.Values.Count > 1)
        {
            lines.Add(GetValueLabel(track, tick.Value));
        }

        lines.AddRange(track.GetDetails?.Invoke(tick) ?? []);

        lines.Add(tick.Node.Symbol);

        lines.Add($"Thread {tick.Thread}  ·  Call {tick.Call + 1:N0}");

        lines.Add(GetPositionLabel(tick.Position));

        return string.Join(Environment.NewLine, lines);
    }

    private string GetPositionLabel(TraceSpan position)
    {
        var origin = Host.GetOrigin(TraceTimelineAxis.Position);

        return $"Position {Math.Floor(position.Start - origin):N0} to {Math.Floor(position.End - origin):N0}";
    }

    private void DrawCallLinks(SKCanvas canvas, double scale)
    {
        foreach (var tickTrack in VisibleTracks)
        {
            if (!tickTrack.HasCallReturnLine)
            {
                continue;
            }

            for (var row = 0; row < Rows.Length; row++)
            {
                for (var track = 0; track < Rows[row].Lifetimes.Length; track++)
                {
                    var thread = Rows[row].Lifetimes[track].Thread;

                    if (tickTrack.Series.GetTicks(Rows[row].Operator, thread) is not { } ticks
                        || GetVisibleTicks(ticks, scale) is not { } visible)
                    {
                        continue;
                    }

                    var width = GetTickWidth(tickTrack, ticks.Count);

                    for (var index = visible.First; index < visible.End; index++)
                    {
                        var tick = ticks.Get(Axis, index);

                        if (tick.Caller is { } caller && GetTrack(caller, thread) is { } callerTrack)
                        {
                            var callLink = new TraceCallLinkHit(row, track, tickTrack, tick);

                            DrawCallLink(canvas, callLink, callerTrack, width, Paints.DottedCallLink, Paints.CallLink, scale);
                        }
                    }
                }
            }
        }
    }

    private (int First, int End)? GetVisibleTicks(TraceMarkIndex<TraceTick> ticks, double scale)
    {
        var first = ticks.GetFirstEndingFrom(Axis, Host.ViewStart);

        var end = SortedSearch.FirstAfter(ticks.GetStarts(Axis), Host.ViewEnd);

        if (end <= first || end - first > (Host.ViewEnd - Host.ViewStart) * scale / MinimumCallLinkWidth + 2)
        {
            return null;
        }

        for (var index = first; index < end; index++)
        {
            var span = ticks.Get(Axis, index).Of(Axis);

            if ((span.End - span.Start) * scale < MinimumCallLinkWidth)
            {
                return null;
            }
        }

        return (first, end);
    }

    private void DrawCallLink(SKCanvas canvas,
                              TraceCallLinkHit callLink,
                              (int Row, int Track) caller,
                              float width,
                              SKPaint call,
                              SKPaint callReturn,
                              double scale)
    {
        var callee = (callLink.Row, callLink.Track);

        var (callerY, calleeY) = GetCallLinkEnds(callee, caller);

        var calleeRow = Rows[callLink.Row];

        var colour = callLink.TickTrack.GetColour(callLink.Tick)
                     ?? GetOperatorColour(calleeRow.Operator, calleeRow.Lifetimes[callLink.Track]);

        call.Color = colour;

        callReturn.Color = colour;

        var (callX, returnX) = GetCallLinkPositions(callLink.Tick, width, scale);

        canvas.DrawLine(callX, callerY, callX, calleeY, call);

        canvas.DrawLine(returnX, calleeY, returnX, callerY, callReturn);
    }

    private void DrawHoverCallLink(SKCanvas canvas, double scale)
    {
        if (Hover.CallLink is not { } hover
            || hover.Row >= Rows.Length
            || hover.Tick.Caller is not { } callerEvent
            || GetTrack(callerEvent, Rows[hover.Row].Lifetimes[hover.Track].Thread) is not { } caller)
        {
            return;
        }

        var row = Rows[hover.Row];

        var count = hover.TickTrack.Series.GetTicks(row.Operator, row.Lifetimes[hover.Track].Thread)?.Count ?? 0;

        var width = GetTickWidth(hover.TickTrack, count);

        var (callerY, calleeY) = GetCallLinkEnds((hover.Row, hover.Track), caller);

        var (callX, returnX) = GetCallLinkPositions(hover.Tick, width, scale);

        Host.PathBuilder.MoveTo(callX, callerY);
        Host.PathBuilder.LineTo(callX, calleeY);
        Host.PathBuilder.MoveTo(returnX, calleeY);
        Host.PathBuilder.LineTo(returnX, callerY);

        using (var shadow = Host.PathBuilder.Detach())
        {
            canvas.Save();

            canvas.Translate(LineageShadowOffset, LineageShadowOffset);

            canvas.DrawPath(shadow, Paints.LineageShadow);

            canvas.Restore();
        }

        DrawCallLink(canvas, hover, caller, width, Paints.HoverDottedCallLink, Paints.HoverCallLink, scale);
    }

    private (float CallX, float ReturnX) GetCallLinkPositions(TraceTick tick, float width, double scale)
    {
        var (left, right) = GetTickEdges(tick.Of(Axis), width, scale);

        return (GetFirstPixelCentre(left), GetLastPixelCentre(right));
    }

    private (float CallerY, float CalleeY) GetCallLinkEnds((int Row, int Track) callee, (int Row, int Track) caller)
    {
        var (calleeTop, calleeHeight) = GetTrackBounds(callee.Row, callee.Track);

        var (callerTop, callerHeight) = GetTrackBounds(caller.Row, caller.Track);

        return callerTop < calleeTop ? (callerTop + callerHeight, calleeTop) : (callerTop, calleeTop + calleeHeight);
    }

    private TraceCallLinkHit? GetCallLinkAt(Point position)
    {
        if (GetOperatorAt(position) is not { } hit)
        {
            return null;
        }

        var scale = Host.Scale;

        var value = Host.ViewStart + position.X / scale;

        var tolerance = TraceTimelineControl.HitTolerance / scale;

        var row = Rows[hit.Row];

        var thread = row.Lifetimes[hit.Track].Thread;

        foreach (var tickTrack in VisibleTracks)
        {
            if (tickTrack.HasCallReturnLine
                && tickTrack.Series.GetTicks(row.Operator, thread) is { } ticks
                && ticks.GetNearest(Axis, value, tolerance) is { Caller: not null } tick)
            {
                return new TraceCallLinkHit(hit.Row, hit.Track, tickTrack, tick);
            }
        }

        return null;
    }

    private (int Row, int Track)? GetTrack(ExecutionOperatorEvent operatorEvent, uint thread)
    {
        if (GetRowIndex(operatorEvent) is not { } row)
        {
            return null;
        }

        var track = Array.FindIndex(Rows[row].Lifetimes, l => l.Thread == thread);

        return track >= 0 ? (row, track) : null;
    }

    private readonly record struct TickStrip(TraceTickTrack Track, TraceMarkIndex<TraceTick> Ticks, float Top, float Height);
}
