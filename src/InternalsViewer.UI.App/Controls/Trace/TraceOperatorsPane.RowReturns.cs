using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;
using InternalsViewer.UI.App.Controls.Timeline;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Trace;

internal sealed partial class TraceOperatorsPane
{
    private const float ReturnStripHeight = 7f;

    private const float ReturnStripShare = 0.35f;

    private const float MinimumReturnStripHeight = 2f;

    private const float ReturnStripShade = 0.5f;

    private const float ReturnStripHoverShade = 0.7f;

    private const float MinimumCallDurationWidth = 1f;

    private const byte CallDurationAlpha = 0x60;

    private const float CallDurationGap = 1f;

    private const float ReturnMarkWidth = 2f;

    private const float MinimumCallLinkWidth = 3f;

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

    public TraceRowReturnMarks? RowReturnMarks { get; set; }

    public bool ShowIteratorCalls { get; set; } = true;

    public (CallStackNode Node, int Call)? GetCallDuration(TraceOperatorHit hit, Point position)
    {
        if (RowReturnMarks is not { } marks)
        {
            return null;
        }

        var scale = Host.Scale;

        var lifetime = Rows[hit.Row].Lifetimes[hit.Track];

        if (marks.GetCall(Rows[hit.Row].Operator, lifetime.Thread, Axis, Host.ViewStart + position.X / scale) is not { } call
            || (call.End - call.Start) * scale <= MinimumCallDurationWidth)
        {
            return null;
        }

        return (call.Node, call.Call);
    }

    private static float GetReturnStripHeight(SKRect bar)
    {
        var height = Math.Min(ReturnStripHeight, bar.Height * ReturnStripShare);

        return height >= MinimumReturnStripHeight ? height : 0;
    }

    private bool HasReturnStrip(TraceOperatorRow row, TraceOperatorLifetime lifetime)
        => ShowIteratorCalls && RowReturnMarks is { } marks && marks.HasMarks(row.Operator, lifetime.Thread);

    private void DrawReturnStrip(SKCanvas canvas,
                                 TraceOperatorRow row,
                                 TraceOperatorLifetime lifetime,
                                 SKRect bar,
                                 SKColor colour,
                                 double scale)
    {
        var height = GetReturnStripHeight(bar);

        if (height <= 0 || !HasReturnStrip(row, lifetime) || RowReturnMarks is not { } marks)
        {
            return;
        }

        var isHovered = Hover.ReturnStrip is { } hover && ReferenceEquals(Rows[hover.Row].Lifetimes[hover.Track], lifetime);

        canvas.Save();

        canvas.ClipRoundRect(Shape, antialias: true);

        Paints.Fill.Color = TimelineColours.Scale(colour, isHovered ? ReturnStripHoverShade : ReturnStripShade);

        canvas.DrawRect(bar.Left, bar.Top, bar.Width, height, Paints.Fill);

        DrawCallDurations(canvas,
                          marks.GetCallStarts(row.Operator, lifetime.Thread, Axis),
                          marks.GetCallEnds(row.Operator, lifetime.Thread, Axis),
                          bar.Top,
                          height,
                          colour.WithAlpha(CallDurationAlpha),
                          scale);

        foreach (var style in ReturnStyles)
        {
            var returns = marks.GetMarks(row.Operator, lifetime.Thread, Axis, style.Type);

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
        Paints.Fill.Color = colour;

        var index = SortedSearch.FirstAtOrAfter(ends, Host.ViewStart);

        while (index < ends.Length && starts[index] <= Host.ViewEnd)
        {
            var left = (float)((starts[index] - Host.ViewStart) * scale);

            var right = (float)((ends[index] - Host.ViewStart) * scale);

            if (right - left > MinimumCallDurationWidth)
            {
                canvas.DrawRect(left, top, right - left - CallDurationGap, height, Paints.Fill);

                index++;
            }
            else
            {
                index = SortedSearch.FirstAtOrAfter(ends, Host.ViewStart + (Math.Floor(right) + 1) / scale, index + 1);
            }
        }
    }

    private TraceOperatorHit? GetReturnStripAt(Point position)
    {
        if (!ShowIteratorCalls || RowReturnMarks is not { } marks || GetOperatorAt(position) is not { } hit)
        {
            return null;
        }

        var lifetime = Rows[hit.Row].Lifetimes[hit.Track];

        if (!marks.HasMarks(Rows[hit.Row].Operator, lifetime.Thread)
            || GetTrackRect(hit.Row, hit.Track, Host.Width, Host.Scale) is not { } bar)
        {
            return null;
        }

        var height = GetReturnStripHeight(bar);

        return height > 0 && position.Y >= bar.Top && position.Y < bar.Top + height ? hit : null;
    }

    private string GetReturnStripToolTip(TraceOperatorHit hit)
    {
        if (RowReturnMarks is not { } marks)
        {
            return ReturnStripTitle;
        }

        var operatorEvent = Rows[hit.Row].Operator;

        var thread = Rows[hit.Row].Lifetimes[hit.Track].Thread;

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

    private void ClickReturnStrip(TraceOperatorHit hit, Point position)
    {
        if (GetCallDuration(hit, position) is not { } duration)
        {
            OpenNearestReturn(hit, position);

            return;
        }

        Host.ClickCall(duration.Node, duration.Call);
    }

    private void OpenNearestReturn(TraceOperatorHit hit, Point position)
    {
        if (RowReturnMarks is not { } marks)
        {
            return;
        }

        var scale = Host.Scale;

        var operatorEvent = Rows[hit.Row].Operator;

        var thread = Rows[hit.Row].Lifetimes[hit.Track].Thread;

        if (marks.GetNearestMark(operatorEvent, thread, Axis, Host.ViewStart + position.X / scale) is { } mark)
        {
            Host.OpenCallTree(mark.Node, mark.Call);
        }
    }

    private void DrawMarks(SKCanvas canvas,
                           ReadOnlySpan<double> values,
                           float top,
                           float height,
                           double scale,
                           SKColor colour)
    {
        if (values.IsEmpty)
        {
            return;
        }

        Paints.Fill.Color = colour;

        var index = SortedSearch.FirstAtOrAfter(values, Host.ViewStart);

        while (index < values.Length && values[index] <= Host.ViewEnd)
        {
            var x = (float)((values[index] - Host.ViewStart) * scale);

            canvas.DrawRect(x, top + 1, ReturnMarkWidth, height - 2, Paints.Fill);

            index = SortedSearch.FirstAtOrAfter(values, Host.ViewStart + (Math.Floor(x) + ReturnMarkWidth) / scale, index + 1);
        }
    }

    private void DrawCallLinks(SKCanvas canvas, double scale)
    {
        if (!ShowIteratorCalls || RowReturnMarks is not { } marks)
        {
            return;
        }

        for (var row = 0; row < Rows.Length; row++)
        {
            for (var track = 0; track < Rows[row].Lifetimes.Length; track++)
            {
                var lifetime = Rows[row].Lifetimes[track];

                if (marks.GetCallLinks(Rows[row].Operator, lifetime.Thread, Axis) is not { } links
                    || GetTrack(links.Caller, lifetime.Thread) is not { } caller
                    || GetVisibleCalls(links, scale) is not { } visible)
                {
                    continue;
                }

                DrawCallLinks(canvas, links, visible.First, visible.End, (row, track), caller, scale);
            }
        }
    }

    private (int First, int End)? GetVisibleCalls(TraceCallLinks links, double scale)
    {
        var first = SortedSearch.FirstAtOrAfter(links.Ends, Host.ViewStart);

        var end = SortedSearch.FirstAfter(links.Starts, Host.ViewEnd);

        if (end <= first || end - first > (Host.ViewEnd - Host.ViewStart) * scale / MinimumCallLinkWidth + 2)
        {
            return null;
        }

        for (var index = first; index < end; index++)
        {
            if ((links.Ends[index] - links.Starts[index]) * scale < MinimumCallLinkWidth)
            {
                return null;
            }
        }

        return (first, end);
    }

    private void DrawCallLinks(SKCanvas canvas,
                               TraceCallLinks links,
                               int first,
                               int end,
                               (int Row, int Track) callee,
                               (int Row, int Track) caller,
                               double scale)
    {
        var (callerY, calleeY) = GetCallLinkEnds(callee, caller);

        SetCallLinkColours(Paints.DottedCallLink, Paints.CallLink, callee, caller);

        for (var index = first; index < end; index++)
        {
            var callX = GetPixelCentre((float)((links.Starts[index] - Host.ViewStart) * scale));

            var returnX = GetPixelCentre((float)((links.Ends[index] - Host.ViewStart) * scale));

            canvas.DrawLine(callX, callerY, callX, calleeY, Paints.DottedCallLink);

            canvas.DrawLine(returnX, calleeY, returnX, callerY, Paints.CallLink);
        }
    }

    private void DrawHoverCallLink(SKCanvas canvas, double scale)
    {
        if (Hover.CallLink is not { } hover
            || hover.Row >= Rows.Length
            || GetCallLinks(hover.Row, hover.Track) is not { } links
            || hover.Index >= links.Starts.Length
            || GetTrack(links.Caller, Rows[hover.Row].Lifetimes[hover.Track].Thread) is not { } caller)
        {
            return;
        }

        var (callerY, calleeY) = GetCallLinkEnds((hover.Row, hover.Track), caller);

        var offset = (Paints.HoverCallLink.StrokeWidth - 1) / 2;

        var callX = GetPixelCentre((float)((links.Starts[hover.Index] - Host.ViewStart) * scale)) + offset;

        var returnX = GetPixelCentre((float)((links.Ends[hover.Index] - Host.ViewStart) * scale)) + offset;

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

        SetCallLinkColours(Paints.HoverDottedCallLink, Paints.HoverCallLink, (hover.Row, hover.Track), caller);

        canvas.DrawLine(callX, callerY, callX, calleeY, Paints.HoverDottedCallLink);

        canvas.DrawLine(returnX, calleeY, returnX, callerY, Paints.HoverCallLink);
    }

    private (float CallerY, float CalleeY) GetCallLinkEnds((int Row, int Track) callee, (int Row, int Track) caller)
    {
        var (calleeTop, calleeHeight) = GetTrackBounds(callee.Row, callee.Track);

        var (callerTop, callerHeight) = GetTrackBounds(caller.Row, caller.Track);

        return callerTop < calleeTop ? (callerTop + callerHeight, calleeTop) : (callerTop, calleeTop + calleeHeight);
    }

    private void SetCallLinkColours(SKPaint call, SKPaint callReturn, (int Row, int Track) callee, (int Row, int Track) caller)
    {
        var callerRow = Rows[caller.Row];

        var calleeRow = Rows[callee.Row];

        call.Color = GetOperatorColour(callerRow.Operator);

        callReturn.Color = GetOperatorColour(calleeRow.Operator);
    }

    private TraceCallLinkHit? GetCallLinkAt(Point position)
    {
        if (!ShowIteratorCalls || GetOperatorAt(position) is not { } hit || GetCallLinks(hit.Row, hit.Track) is not { } links)
        {
            return null;
        }

        var scale = Host.Scale;

        var value = Host.ViewStart + position.X / scale;

        var tolerance = TraceTimelineControl.HitTolerance / scale;

        var index = SortedSearch.FirstAtOrAfter(links.Ends, value - tolerance);

        return index < links.Starts.Length && links.Starts[index] - tolerance <= value ? new TraceCallLinkHit(hit.Row, hit.Track, index) : null;
    }

    private TraceCallLinks? GetCallLinks(int row, int track)
        => RowReturnMarks?.GetCallLinks(Rows[row].Operator, Rows[row].Lifetimes[track].Thread, Axis);

    private static float GetPixelCentre(float x) => MathF.Ceiling(x - 0.5f) + 0.5f;

    private (int Row, int Track)? GetTrack(ExecutionOperatorEvent operatorEvent, uint thread)
    {
        if (GetRowIndex(operatorEvent) is not { } row)
        {
            return null;
        }

        var track = Array.FindIndex(Rows[row].Lifetimes, l => l.Thread == thread);

        return track >= 0 ? (row, track) : null;
    }

    private readonly record struct ReturnStyle(RowReturnType Type, SKColor? Colour, string Label);
}
