using System;
using System.Collections.Generic;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using SkiaSharp;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Trace;

internal sealed class TraceReadsPane(ITraceTimelineHost host, Func<(float Top, float Height)> getBounds)
{
    private const float LanePadding = 2f;

    private const float MarkerWidth = 1f;

    private const float SparseMarkerWidth = 4f;

    private const int SparseReadCount = 25;

    private const byte DurationAlpha = 96;

    private const float LabelPadding = 4f;

    private const string Title = "Reads";

    private const float MinimumCallRailGap = 4f;

    private const float PageRailGap = 3f;

    private const byte RailAlpha = 150;

    private static readonly SKColor PaneColour = new(30, 30, 30, 220);

    private static readonly SKColor ReadColour = SKColor.Parse(SymbolCategory.BufferPool.GetCategoryMetadata()!.ForegroundColor);

    private static readonly SKColor HoverColour = new(255, 255, 255, 200);

    public TraceReads? Reads { get; set; }

    public TraceRead? Hover { get; set; }

    public bool HasReads => Reads is { Count: > 0 };

    private ITraceTimelineHost Host { get; } = host;

    private Func<(float Top, float Height)> GetBounds { get; } = getBounds;

    private TraceTimelinePaints Paints => Host.Paints;

    private float Top => GetBounds().Top;

    private float Height => GetBounds().Height;

    public bool Contains(Point position) => HasReads && position.Y >= Top && position.Y < Top + Height;

    public TraceRead? GetReadAt(Point position)
    {
        if (!Contains(position) || Reads is not { } reads)
        {
            return null;
        }

        var scale = Host.Scale;

        return reads.GetNearest(Host.Axis, Host.ViewStart + position.X / scale, TraceTimelineControl.HitTolerance / scale);
    }

    public string? GetToolTip(TraceRead? read, Point position)
    {
        if (read is null)
        {
            return Contains(position) ? Title : null;
        }

        var origin = Host.GetOrigin(TraceTimelineAxis.Position);

        var lines = new List<string>
        {
            read.Type == TraceReadType.ReadAhead
                ? $"Read Ahead {read.PageCount:N0} {(read.PageCount == 1 ? "Page" : "Pages")} From {read.Page}"
                : $"Page {read.Page}",
            read.Node.Symbol,
            read.Operator is { } operatorEvent ? GetOperatorTitle(operatorEvent) : "No Operator",
            $"Thread {read.Thread}  ·  Call {read.Call + 1:N0}",
            $"Position {Math.Floor(read.Position.Start - origin):N0} to {Math.Floor(read.Position.End - origin):N0}"
        };

        return string.Join(Environment.NewLine, lines);
    }

    public void Click(Point position)
    {
        if (GetReadAt(position) is { } read)
        {
            Host.ClickCall(read.Node, read.Call);
        }
    }

    public void Draw(SKCanvas canvas, int width, double scale)
    {
        Paints.Fill.Color = PaneColour;

        canvas.DrawRect(0, Top, width, Height, Paints.Fill);

        if (Reads is not { } reads)
        {
            return;
        }

        var axis = Host.Axis;

        var viewStart = Host.ViewStart;

        var viewEnd = Host.ViewEnd;

        var starts = reads.GetStarts(axis);

        var top = Top + LanePadding;

        var height = Math.Max(Height - LanePadding * 2, 1);

        var markerWidth = GetMarkerWidth();

        var nextPagePixel = float.MinValue;

        canvas.Save();

        canvas.ClipRect(new SKRect(0, Top, width, Top + Height));

        for (var index = reads.GetFirstEndingFrom(axis, viewStart); index < starts.Length && starts[index] <= viewEnd; index++)
        {
            var read = reads.GetRead(axis, index);

            var span = read.Of(axis);

            var left = (float)((span.Start - viewStart) * scale);

            var right = Math.Max((float)((span.End - viewStart) * scale), left + markerWidth);

            if (read.Type == TraceReadType.Page && left < nextPagePixel)
            {
                continue;
            }

            DrawRead(canvas, read, new SKRect(left, top, right, top + height), markerWidth);

            if (read.Type == TraceReadType.Page)
            {
                nextPagePixel = MathF.Floor(right) + 1;
            }
        }

        canvas.Restore();
    }

    public void DrawOverlay(SKCanvas canvas, int width, double scale, Func<ExecutionOperatorEvent, uint, float?>? getOrigin)
    {
        if (Hover is not { } read)
        {
            return;
        }

        var span = read.Of(Host.Axis);

        var left = (float)((span.Start - Host.ViewStart) * scale);

        var right = Math.Max((float)((span.End - Host.ViewStart) * scale), left + GetMarkerWidth());

        var top = Top + LanePadding;

        var bottom = Top + Height - LanePadding;

        if (read.Operator is { } operatorEvent && getOrigin?.Invoke(operatorEvent, read.Thread) is { } origin)
        {
            DrawRails(canvas, new ReadRail(left, right, origin, read.PageCount), top, bottom);
        }

        Paints.Outline.Color = HoverColour;

        Paints.Outline.StrokeWidth = 1f;

        canvas.Save();

        canvas.ClipRect(new SKRect(0, Top, width, Top + Height));

        canvas.DrawRect(new SKRect(left, top, right, bottom), Paints.Outline);

        canvas.Restore();
    }

    private float GetMarkerWidth() => Reads is { Count: < SparseReadCount } ? SparseMarkerWidth : MarkerWidth;

    private void DrawRails(SKCanvas canvas, ReadRail rail, float top, float bottom)
    {
        Paints.ReadCallRail.Color = ReadColour.WithAlpha(RailAlpha);

        Paints.ReadReturnRail.Color = ReadColour.WithAlpha(RailAlpha);

        if (rail.End - rail.Start > MinimumCallRailGap)
        {
            canvas.DrawLine(rail.Start, rail.Origin, rail.Start, top, Paints.ReadCallRail);
        }

        for (var page = 0; page < rail.Pages; page++)
        {
            var x = Math.Max(rail.Start, rail.End - page * PageRailGap);

            canvas.DrawLine(x, rail.Origin, x, bottom, Paints.ReadReturnRail);

            if (x <= rail.Start)
            {
                break;
            }
        }
    }

    private void DrawRead(SKCanvas canvas, TraceRead read, SKRect rect, float markerWidth)
    {
        if (rect.Width > markerWidth)
        {
            Paints.Fill.Color = ReadColour.WithAlpha(Math.Min(ReadColour.Alpha, DurationAlpha));

            canvas.DrawRect(rect, Paints.Fill);
        }

        Paints.Fill.Color = ReadColour;

        canvas.DrawRect(rect.Right - markerWidth, rect.Top, markerWidth, rect.Height, Paints.Fill);
    }

    private static string GetOperatorTitle(ExecutionOperatorEvent operatorEvent)
        => operatorEvent.PlanNodeIdentifier is { } identifier
            ? $"{operatorEvent.OperatorDescription} (Node {identifier.NodeId})"
            : operatorEvent.OperatorDescription;

    private readonly record struct ReadRail(float Start, float End, float Origin, int Pages);
}
