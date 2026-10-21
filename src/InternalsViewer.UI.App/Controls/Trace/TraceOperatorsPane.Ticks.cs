using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
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

    private const float BandLabelGap = 1f;

    private const float BandLabelPadding = 2f;

    private const float BandLabelInset = 5f;

    private const float BorderOffset = 0.5f;

    private static readonly SKColor TickHoverColour = new(255, 255, 255, 200);

    private static readonly SKColor BandHoverColour = new(255, 255, 255, 0x80);

    private static readonly SKColor BandLabelBorderColour = new(0, 0, 0, 0x40);

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

    private static SKColor GetStripBackground(TraceTickTrack track,
                                              ExecutionOperatorEvent operatorEvent,
                                              SKColor colour,
                                              bool isHovered)
        => GetBackground(track, operatorEvent) is { } background
            ? isHovered ? background.WithAlpha((byte)Math.Min(background.Alpha * BackgroundHoverAlpha, byte.MaxValue)) : background
            : TimelineColours.Scale(colour, isHovered ? StripHoverShade : StripShade);

    private static SKColor? GetBackground(TraceTickTrack track, ExecutionOperatorEvent operatorEvent)
        => track.Background
           ?? (GetTrackColour(track, operatorEvent) is { } trackColour && TimelineColours.IsDark(trackColour)
                   ? TraceTickTracks.LightBackground
                   : null);

    private static SKColor? GetTrackColour(TraceTickTrack track, ExecutionOperatorEvent operatorEvent)
        => track.GetTrackColour?.Invoke(operatorEvent) ?? track.Colour;

    private static float GetTickWidth(TraceTickTrack track, int count)
        => (count < SparseTickCount ? Math.Max(SparseTickWidth, track.TickWidth) : track.TickWidth) * track.WidthFactor;

    private static float GetFirstPixelCentre(float left) => MathF.Ceiling(left - 0.5f) + 0.5f;

    private static float GetLastPixelCentre(float right) => MathF.Ceiling(right - 0.5f) - 0.5f;

    private (float Left, float Right) GetTickEdges(TraceSpan span, float width, double scale)
    {
        var left = (float)((TraceScale.GetValue(span.From, Axis) - Host.ViewStart) * scale);

        return (left, Math.Max((float)((TraceScale.GetValue(span.To, Axis) - Host.ViewStart) * scale), left + width));
    }

    private double GetLength(TraceSpan span) => TraceScale.GetValue(span.To, Axis) - TraceScale.GetValue(span.From, Axis);

    private List<TickStrip> GetStrips(int row, int track, SKRect bar)
    {
        var strips = new List<TickStrip>();

        if (bar.Height < MinimumStripHeight)
        {
            return strips;
        }

        var baseHeight = GetStripHeight(bar);

        var operatorRow = Rows[row];

        var thread = operatorRow.Lifetimes[track].Thread;

        var top = 0f;

        var bottom = 0f;

        foreach (var tickTrack in VisibleTracks)
        {
            if ((operatorRow.IsStatement && !tickTrack.IsStatementTrack)
                || tickTrack.Series.GetTicks(operatorRow.TickOperator, thread) is not { Count: > 0 } ticks)
            {
                continue;
            }

            var wanted = BandHeights.TryGetValue(tickTrack.Name, out var set) ? set : baseHeight * tickTrack.HeightFactor;

            var height = Math.Min(wanted, bar.Height - top - bottom);

            if (height < MinimumStripHeight)
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

            Paints.Fill.Color = GetStripBackground(strip.Track, Rows[row].TickOperator, colour, isHovered);

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
            Paints.Fill.Color = GetStripBackground(strip.Track, Rows[row].TickOperator, colour, false);

            canvas.DrawRect(rect.Left, strip.Top, rect.Width, strip.Height, Paints.Fill);
        }
    }

    private void DrawTicks(SKCanvas canvas, TickStrip strip, byte value, SKColor operatorColour, float width, double scale)
    {
        var ticks = strip.Ticks;

        var top = strip.Top + 1;

        var height = strip.Height - 2;

        var nextPixel = float.MinValue;

        var first = ticks.GetFirstEndingFrom(Axis, Host.ViewStart);

        for (var index = first; index < ticks.Count && ticks.GetStart(Axis, index) <= Host.ViewEnd; index++)
        {
            var tick = ticks.Get(index);

            if (tick.Value != value)
            {
                continue;
            }

            var (left, right) = GetTickEdges(tick.Span, width, scale);

            var spanPixels = GetLength(tick.Span) * scale;

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

            Paints.Fill.Color = colour.WithAlpha(byte.MaxValue);

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

    private HoveredStrip? GetHoveredStrip(double scale)
    {
        if (Hover.Tick is not { } hover
            || hover.Row >= Rows.Length
            || GetTrackRect(hover.Row, hover.Track, Host.Width, scale) is not { } bar)
        {
            return null;
        }

        foreach (var strip in GetStrips(hover.Row, hover.Track, bar))
        {
            if (ReferenceEquals(strip.Track, hover.TickTrack))
            {
                return new HoveredStrip(hover, strip, bar);
            }
        }

        return null;
    }

    private void DrawTickHover(SKCanvas canvas, double scale)
    {
        if (GetHoveredStrip(scale) is not { Hit.Tick: { } tick } hovered)
        {
            return;
        }

        var strip = hovered.Strip;

        var (left, right) = GetTickEdges(tick.Span, GetTickWidth(strip.Track, strip.Ticks.Count), scale);

        Paints.Outline.Color = TickHoverColour;

        Paints.Outline.StrokeWidth = 1f;

        canvas.DrawRect(new SKRect(left, strip.Top, right, strip.Top + strip.Height), Paints.Outline);
    }

    private static SKRect Snap(SKRect rect)
        => new(MathF.Round(rect.Left), MathF.Round(rect.Top), MathF.Round(rect.Right), MathF.Round(rect.Bottom));

    private void DrawBandHover(SKCanvas canvas, double scale)
    {
        if (GetHoveredStrip(scale) is not { } hovered)
        {
            return;
        }

        var (hit, strip, bar) = hovered;

        var band = Snap(new SKRect(bar.Left, strip.Top, bar.Right, strip.Top + strip.Height));

        Paints.Outline.Color = BandHoverColour;

        Paints.Outline.StrokeWidth = 1f;

        canvas.DrawRect(SKRect.Inflate(band, BorderOffset, BorderOffset), Paints.Outline);

        DrawBandLabel(canvas, hit, strip, band);
    }

    private void DrawBandLabel(SKCanvas canvas, TraceTickHit hit, TickStrip strip, SKRect band)
    {
        var text = hit.TickTrack.GetTitle?.Invoke(Rows[hit.Row].TickOperator) ?? hit.TickTrack.Name;

        var colour = GetTickColour(hit.TickTrack, hit.Tick, hit.Row, hit.Track);

        var font = Paints.OperatorFont;

        var metrics = font.Metrics;

        var height = MathF.Ceiling(metrics.Descent - metrics.Ascent + BandLabelPadding * 2);

        var left = MathF.Max(band.Left, 0);

        var top = strip.Track.Location == TraceTickLocation.Top
            ? band.Bottom + BandLabelGap
            : band.Top - BandLabelGap - height;

        var rect = new SKRect(left, top, MathF.Ceiling(left + font.MeasureText(text) + BandLabelInset * 2), top + height);

        Paints.Fill.Color = colour;

        canvas.DrawRect(rect, Paints.Fill);

        Paints.Outline.Color = BandLabelBorderColour;

        canvas.DrawRect(SKRect.Inflate(rect, -BorderOffset, -BorderOffset), Paints.Outline);

        Paints.Text.Color = TimelineColours.IsLight(colour) ? SKColors.Black : SKColors.White;

        canvas.DrawText(text, rect.Left + BandLabelInset, rect.Top + BandLabelPadding - metrics.Ascent, SKTextAlign.Left, font, Paints.Text);
    }

    private string GetTickToolTip(TraceTickHit hit)
    {
        var row = Rows[hit.Row];

        var track = hit.TickTrack;

        var lines = new List<string> { track.GetTitle?.Invoke(row.TickOperator) ?? track.Name };

        if (hit.Tick is not { } tick)
        {
            var ticks = track.Series.GetTicks(row.TickOperator, row.Lifetimes[hit.Track].Thread)?.All ?? [];

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

        lines.Add($"Thread {tick.Thread}  ·  Call {tick.CallIndex + 1:N0}");

        lines.Add(GetPositionLabel(tick.Span));

        return string.Join(Environment.NewLine, lines);
    }

    private string GetPositionLabel(TraceSpan span)
    {
        var origin = Host.GetOrigin(TraceTimelineAxis.Position);

        var start = TraceScale.GetValue(span.From.Position) - origin;

        var end = TraceScale.GetValue(span.To.Position) - origin;

        return $"Position {Math.Floor(start):N0} to {Math.Floor(end):N0}";
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
                if (Rows[row].IsStatement)
                {
                    continue;
                }

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
                        var tick = ticks.Get(index);

                        if (GetCallerTrack(tick, row, thread) is { } callerTrack)
                        {
                            DrawCallLink(canvas, tickTrack, tick, (row, track), callerTrack, width, scale);
                        }
                    }
                }
            }
        }
    }

    private (int First, int End)? GetVisibleTicks(TraceMarkIndex<TraceTick> ticks, double scale)
    {
        var first = ticks.GetFirstEndingFrom(Axis, Host.ViewStart);

        var end = ticks.GetFirstStartingAfter(Axis, Host.ViewEnd);

        if (end <= first || end - first > (Host.ViewEnd - Host.ViewStart) * scale / MinimumCallLinkWidth + 2)
        {
            return null;
        }

        for (var index = first; index < end; index++)
        {
            if (GetLength(ticks.Get(index).Span) * scale < MinimumCallLinkWidth)
            {
                return null;
            }
        }

        return (first, end);
    }

    private void DrawCallLink(SKCanvas canvas,
                              TraceTickTrack tickTrack,
                              TraceTick tick,
                              (int Row, int Track) callee,
                              (int Row, int Track) caller,
                              float width,
                              double scale)
    {
        var (callerY, calleeY) = GetCallLinkEnds(callee, caller);

        var calleeRow = Rows[callee.Row];

        Paints.DottedCallLink.Color = GetTrackColour(tickTrack, calleeRow.TickOperator)
                                      ?? GetOperatorColour(calleeRow.Operator, calleeRow.Lifetimes[callee.Track]);

        Paints.CallLink.Color = GetTickColour(tickTrack, tick, callee.Row, callee.Track);

        var (callX, returnX) = GetCallLinkPositions(tick, width, scale);

        canvas.DrawLine(callX, callerY, callX, calleeY, Paints.DottedCallLink);

        canvas.DrawLine(returnX, calleeY, returnX, callerY, Paints.CallLink);
    }

    private SKColor GetTickColour(TraceTickTrack tickTrack, TraceTick? tick, int row, int track)
        => (tick is null ? null : tickTrack.GetColour(tick)) ?? GetOperatorColour(Rows[row].Operator, Rows[row].Lifetimes[track]);

    private (float CallX, float ReturnX) GetCallLinkPositions(TraceTick tick, float width, double scale)
    {
        var (left, right) = GetTickEdges(tick.Span, width, scale);

        return (GetFirstPixelCentre(left), GetLastPixelCentre(right));
    }

    private (float CallerY, float CalleeY) GetCallLinkEnds((int Row, int Track) callee, (int Row, int Track) caller)
    {
        var (calleeTop, calleeHeight) = GetTrackBounds(callee.Row, callee.Track);

        var (callerTop, callerHeight) = GetTrackBounds(caller.Row, caller.Track);

        return callerTop < calleeTop ? (callerTop + callerHeight, calleeTop) : (callerTop, calleeTop + calleeHeight);
    }

    private (int Row, int Track)? GetCallerTrack(TraceTick tick, int row, uint thread)
        => tick.Caller is { } caller ? GetTrack(caller, thread) : GetStatementTrack(row, thread);

    private (int Row, int Track)? GetStatementTrack(int row, uint thread)
    {
        if (row == 0 || !Rows[0].IsStatement || !ReferenceEquals(Rows[0].TickOperator, Rows[row].Operator))
        {
            return null;
        }

        var track = Array.FindIndex(Rows[0].Lifetimes, l => l.Thread == thread);

        return track >= 0 ? (0, track) : null;
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

    private readonly record struct HoveredStrip(TraceTickHit Hit, TickStrip Strip, SKRect Bar);
}
