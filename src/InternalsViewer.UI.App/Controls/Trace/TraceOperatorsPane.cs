using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Timeline;
using InternalsViewer.UI.App.Controls.Timeline.Renderers;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query;
using SkiaSharp;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Trace;

internal sealed partial class TraceOperatorsPane(ITraceTimelineHost host, Func<(float Top, float Height)> getBounds) : IDisposable
{
    private const float OperatorsPadding = 4f;

    private const float OperatorsBottomGap = 2f;

    private const float OperatorRowGap = 10f;

    private const float OperatorBarPadding = 1f;

    private const float OperatorBarShare = 0.15f;

    private const float OperatorCornerRadius = 3f;

    private const float OperatorGradientLift = 0.04f;

    private const float OperatorSelectedStroke = 2f;

    private const byte LifespanAlpha = 120;

    private const string ToolTipTitle = "Operators";

    private static readonly SKColor OperatorPaneColour = new(30, 30, 30, 220);

    private static readonly SKColor OperatorLabelColour = new(235, 235, 235);

    private static readonly SKColor OperatorSelectedColour = new(255, 255, 255, 230);

    private static readonly SKColor OperatorHoverColour = new(255, 255, 255, 150);

    private static readonly SKColor OperatorDimColour = new(0, 0, 0, 160);

    public TraceOperatorRow[] Rows { get; private set; } = [];

    public bool HasRows => Rows.Length > 0;

    public ExecutionOperatorEvent? SelectedOperator { get; private set; }

    public EventColourProvider? ColourProvider { get; set; }

    public TraceOperatorsHover Hover { get; set; } = TraceOperatorsHover.None;

    private ITraceTimelineHost Host { get; } = host;

    private Func<(float Top, float Height)> GetBounds { get; } = getBounds;

    private Dictionary<TraceOperatorLifetime, string> Details { get; } = new(ReferenceEqualityComparer.Instance);

    private Dictionary<SKColor, SKShader> Gradients { get; } = [];

    private SKRoundRect Shape { get; } = new();

    private TraceTimelinePaints Paints => Host.Paints;

    private float Top => GetBounds().Top;

    private float Height => GetBounds().Height;

    private TraceTimelineAxis Axis => Host.Axis;

    private float RowHeight
    {
        get
        {
            var rows = Math.Max(Rows.Length, 1);

            var spacing = OperatorsPadding * 2 + OperatorsBottomGap + OutputRowsArea + (rows - 1) * OperatorRowGap;

            return Math.Max((Height - spacing) / rows, 1);
        }
    }

    public void Rebuild(TraceTimeline? timeline)
    {
        Rows = GetOperatorRows(timeline);

        Details.Clear();

        Hover = TraceOperatorsHover.None;

        CachedBlocks = null;

        if (SelectedOperator is { } selected && !Rows.Any(r => ReferenceEquals(r.Operator, selected)))
        {
            SelectedOperator = null;
        }
    }

    public void ClearSelectedOperator()
    {
        if (SelectedOperator is null)
        {
            return;
        }

        SelectedOperator = null;

        Host.OnSelectedOperatorChanged(null);
    }

    public bool Contains(Point position) => HasRows && position.Y >= Top && position.Y < Top + Height;

    public void Draw(SKCanvas canvas, int width, double scale)
    {
        Paints.Fill.Color = OperatorPaneColour;

        canvas.DrawRect(0, Top, width, Height, Paints.Fill);

        canvas.Save();

        canvas.ClipRect(new SKRect(0, Top, width, Top + Height));

        DrawOutputRows(canvas, scale);

        for (var row = 0; row < Rows.Length; row++)
        {
            for (var track = 0; track < Rows[row].Lifetimes.Length; track++)
            {
                var lifetime = Rows[row].Lifetimes[track];

                if (GetLifespanRect(row, track, width, scale) is { } lifespan)
                {
                    DrawLifespan(canvas, Rows[row], lifetime, lifespan);
                }

                if (GetTrackRect(row, track, width, scale) is { } rect)
                {
                    DrawOperatorBar(canvas, Rows[row], lifetime, rect, scale);
                }
            }
        }

        DrawCallLinks(canvas, scale);

        canvas.Restore();
    }

    public void DrawOverlay(SKCanvas canvas, int width, double scale)
    {
        canvas.Save();

        canvas.ClipRect(new SKRect(0, Top, width, Top + Height));

        DimOperators(canvas, width);

        if (Hover.Operator is { } hover && hover.Row < Rows.Length)
        {
            OutlineTrack(canvas, hover.Row, hover.Track, width, scale, OperatorHoverColour, 1f);
        }

        for (var row = 0; row < Rows.Length; row++)
        {
            if (!ReferenceEquals(Rows[row].Operator, SelectedOperator))
            {
                continue;
            }

            for (var track = 0; track < Rows[row].Lifetimes.Length; track++)
            {
                OutlineTrack(canvas, row, track, width, scale, OperatorSelectedColour, OperatorSelectedStroke);
            }
        }

        DrawReadHover(canvas, scale);

        DrawOutputRowLineage(canvas, width, scale);

        DrawHoverCallLink(canvas, scale);

        canvas.Restore();
    }

    public TraceOperatorsHover GetHover(Point position)
    {
        if (!Contains(position))
        {
            return TraceOperatorsHover.None;
        }

        var outputRow = GetOutputRow(position);

        var returnStrip = outputRow is null ? GetReturnStripAt(position) : null;

        var read = outputRow is null && returnStrip is null ? GetReadAt(position) : null;

        var callLink = outputRow is null ? GetCallLinkAt(position) : null;

        var operatorHit = returnStrip is null && outputRow is null && read is null ? GetOperatorAt(position) : null;

        return new TraceOperatorsHover(operatorHit, returnStrip, callLink, outputRow, read);
    }

    public string? GetToolTip(TraceOperatorsHover hover, Point position)
    {
        if (hover.Operator is { } operatorHit)
        {
            return GetDetails(operatorHit);
        }

        if (hover.OutputRow is not null)
        {
            return null;
        }

        if (hover.ReturnStrip is { } strip)
        {
            return GetReturnStripToolTip(strip);
        }

        if (hover.Read is { } read)
        {
            return GetReadToolTip(read);
        }

        return Contains(position) ? ToolTipTitle : null;
    }

    public TraceOperatorHit? GetOperatorAt(Point position)
    {
        if (GetRowAt(position) is not { } found)
        {
            return null;
        }

        var (row, inRow) = found;

        var lifetimes = Rows[row].Lifetimes;

        var track = Math.Clamp((int)(inRow / (RowHeight / lifetimes.Length)), 0, lifetimes.Length - 1);

        var scale = Host.Scale;

        var value = Host.ViewStart + position.X / scale;

        var tolerance = TraceTimelineControl.HitTolerance / scale;

        return lifetimes[track].GetConstructed(Axis) - tolerance <= value && value <= lifetimes[track].GetDestroyed(Axis) + tolerance
            ? new TraceOperatorHit(row, track)
            : null;
    }

    public bool IsBackground(Point position)
        => GetOperatorAt(position) is null && GetOutputRow(position) is null && GetReturnStripAt(position) is null;

    public (double Start, double End) GetActiveRange(TraceOperatorHit hit)
    {
        var lifetime = Rows[hit.Row].Lifetimes[hit.Track];

        return (lifetime.StartOf(Axis), lifetime.EndOf(Axis));
    }

    public PlanNodeIdentifier? GetPlanNode(Point position)
        => GetOperatorAt(position) is { } hit ? Rows[hit.Row].Operator.PlanNodeIdentifier : null;

    public void Click(Point position)
    {
        if (GetOutputRow(position) is { } outputRow)
        {
            Host.MovePlayhead(outputRow.Return.GetValue(Axis));
        }
        else if (GetReturnStripAt(position) is { } strip)
        {
            ClickReturnStrip(strip, position);
        }
        else if (GetReadAt(position) is { } read)
        {
            Host.ClickCall(read.Node, read.Call);
        }
        else
        {
            SelectOperator(GetOperatorAt(position));
        }
    }

    public void Dispose()
    {
        Shape.Dispose();

        foreach (var gradient in Gradients.Values)
        {
            gradient.Dispose();
        }

        Gradients.Clear();
    }

    private static TraceOperatorRow[] GetOperatorRows(TraceTimeline? timeline)
    {
        if (timeline is null)
        {
            return [];
        }

        var lanes = new Dictionary<uint, int>();

        for (var lane = 0; lane < timeline.Threads.Count; lane++)
        {
            lanes[timeline.Threads[lane].ThreadId] = lane;
        }

        return [.. timeline.Lifetimes
                           .Where(l => lanes.ContainsKey(l.Thread))
                           .GroupBy(l => l.Operator, ReferenceEqualityComparer.Instance)
                           .Select(g => CreateRow((ExecutionOperatorEvent)g.Key!, [.. g.OrderBy(l => lanes[l.Thread])]))
                           .OrderBy(r => r.Operator.NodeLevel)
                           .ThenBy(r => r.Lifetimes.Min(l => l.StartOf(TraceTimelineAxis.Position)))
                           .ThenBy(r => r.Operator.PlanNodeIdentifier?.NodeId ?? 0)];
    }

    private static TraceOperatorRow CreateRow(ExecutionOperatorEvent operatorEvent, TraceOperatorLifetime[] lifetimes)
    {
        var target = operatorEvent.ObjectName.Length > 0
            ? operatorEvent.ObjectName
            : operatorEvent.LogicalOperator != operatorEvent.Name ? operatorEvent.LogicalOperator : string.Empty;

        var title = operatorEvent.PlanNodeIdentifier is { } identifier
            ? $"{operatorEvent.OperatorDescription} (Node {identifier.NodeId})"
            : operatorEvent.OperatorDescription;

        return new TraceOperatorRow(operatorEvent, lifetimes, operatorEvent.Name, target, title);
    }

    private void DrawOperatorBar(SKCanvas canvas, TraceOperatorRow row, TraceOperatorLifetime lifetime, SKRect rect, double scale)
    {
        var colour = GetOperatorColour(row.Operator, lifetime);

        var radius = Math.Min(rect.Height / 2, OperatorCornerRadius);

        Shape.SetRect(rect, radius, radius);

        canvas.Save();

        canvas.ClipRoundRect(Shape, antialias: true);

        canvas.Translate(0, rect.Top);

        canvas.Scale(1, rect.Height);

        Paints.Fill.Color = colour;

        Paints.Fill.Shader = GetGradient(colour);

        canvas.DrawRect(rect.Left, 0, rect.Width, 1, Paints.Fill);

        Paints.Fill.Shader = null;

        canvas.Restore();

        DrawReturnStrip(canvas, row, lifetime, rect, colour, scale);

        DrawReadStrip(canvas, row, lifetime, rect, colour, scale);

        var stripHeight = HasReturnStrip(row, lifetime) ? GetReturnStripHeight(rect) : 0;

        var readsHeight = HasReadStrip(row, lifetime) ? GetReturnStripHeight(rect) : 0;

        DrawOperatorLabel(canvas, row, rect with { Top = rect.Top + stripHeight, Bottom = rect.Bottom - readsHeight });
    }

    private void DrawLifespan(SKCanvas canvas, TraceOperatorRow row, TraceOperatorLifetime lifetime, SKRect rect)
    {
        var colour = GetOperatorColour(row.Operator, lifetime);

        var radius = Math.Min(rect.Height / 2, OperatorCornerRadius);

        Shape.SetRect(rect, radius, radius);

        canvas.Save();

        canvas.ClipRoundRect(Shape, antialias: true);

        Paints.Fill.Color = colour.WithAlpha(LifespanAlpha);

        canvas.DrawRect(rect, Paints.Fill);

        var stripHeight = GetReturnStripHeight(rect);

        if (stripHeight > 0 && HasReturnStrip(row, lifetime))
        {
            Paints.Fill.Color = TimelineColours.Scale(colour, ReturnStripShade);

            canvas.DrawRect(rect.Left, rect.Top, rect.Width, stripHeight, Paints.Fill);
        }

        if (stripHeight > 0 && HasReadStrip(row, lifetime))
        {
            Paints.Fill.Color = TimelineColours.Scale(colour, ReturnStripShade);

            canvas.DrawRect(rect.Left, rect.Bottom - stripHeight, rect.Width, stripHeight, Paints.Fill);
        }

        canvas.Restore();
    }

    private SKShader GetGradient(SKColor colour)
    {
        if (!Gradients.TryGetValue(colour, out var gradient))
        {
            gradient = SKShader.CreateLinearGradient(new SKPoint(0, 0),
                                                     new SKPoint(0, 1),
                                                     [
                                                         TimelineColours.Scale(colour, 1f + OperatorGradientLift),
                                                         TimelineColours.Scale(colour, 1f - OperatorGradientLift)
                                                     ],
                                                     null,
                                                     SKShaderTileMode.Clamp);

            Gradients[colour] = gradient;
        }

        return gradient;
    }

    private void DrawOperatorLabel(SKCanvas canvas, TraceOperatorRow row, SKRect rect)
    {
        Paints.Text.Color = OperatorLabelColour;

        canvas.Save();

        canvas.ClipRect(rect);

        OperatorLabelRenderer.Draw(canvas,
                                   row.Name,
                                   row.Target,
                                   Math.Max(rect.Left, 0),
                                   rect.Right,
                                   rect.MidY,
                                   rect.Height,
                                   Paints.OperatorBoldFont,
                                   Paints.OperatorFont,
                                   Paints.Text);

        canvas.Restore();
    }

    private SKRect? GetTrackRect(int row, int track, int width, double scale)
    {
        var lifetime = Rows[row].Lifetimes[track];

        return GetSpanRect(row, track, lifetime.StartOf(Axis), lifetime.EndOf(Axis), width, scale);
    }

    private SKRect? GetLifespanRect(int row, int track, int width, double scale)
    {
        var lifetime = Rows[row].Lifetimes[track];

        return GetSpanRect(row, track, lifetime.GetConstructed(Axis), lifetime.GetDestroyed(Axis), width, scale);
    }

    private SKRect? GetSpanRect(int row, int track, double start, double end, int width, double scale)
    {
        if (end < Host.ViewStart || start > Host.ViewEnd)
        {
            return null;
        }

        var (top, height) = GetTrackBounds(row, track);

        var left = (float)((start - Host.ViewStart) * scale);

        var right = Math.Max((float)((end - Host.ViewStart) * scale), left + TraceTimelineControl.NarrowSpan * 2);

        return new SKRect(Math.Max(left, -OperatorCornerRadius * 2),
                          top,
                          Math.Min(right, width + OperatorCornerRadius * 2),
                          top + height);
    }

    private (float Top, float Height) GetTrackBounds(int row, int track)
    {
        var trackHeight = RowHeight / Rows[row].Lifetimes.Length;

        var padding = Math.Min(OperatorBarPadding, trackHeight * OperatorBarShare);

        return (GetRowTop(row) + track * trackHeight + padding, Math.Max(trackHeight - padding * 2, 1));
    }

    private float GetRowTop(int row) => Top + OperatorsPadding + OutputRowsArea + row * (RowHeight + OperatorRowGap);

    private SKColor GetOperatorColour(ExecutionOperatorEvent operatorEvent, TraceOperatorLifetime lifetime)
        => ColourProvider is { } colours ? colours.GetColour(operatorEvent).ToSkColor() : Host.GetColour(lifetime.Node);

    private void DimOperators(SKCanvas canvas, int width)
    {
        if (SelectedOperator is null || Host.ShowCallStack)
        {
            return;
        }

        var rowHeight = RowHeight;

        for (var row = 0; row < Rows.Length; row++)
        {
            if (ReferenceEquals(Rows[row].Operator, SelectedOperator))
            {
                var top = GetRowTop(row);

                Host.PathBuilder.AddRect(new SKRect(0, top, width, top + rowHeight));
            }
        }

        using var selected = Host.PathBuilder.Detach();

        canvas.Save();

        canvas.ClipPath(selected, SKClipOperation.Difference);

        Paints.Fill.Color = OperatorDimColour;

        canvas.DrawRect(0, Top, width, Height, Paints.Fill);

        canvas.Restore();
    }

    private void OutlineTrack(SKCanvas canvas, int row, int track, int width, double scale, SKColor colour, float stroke)
    {
        if (GetTrackRect(row, track, width, scale) is not { } rect)
        {
            return;
        }

        var radius = Math.Min(rect.Height / 2, OperatorCornerRadius);

        Paints.Outline.Color = colour;

        Paints.Outline.StrokeWidth = stroke;

        canvas.DrawRoundRect(rect, radius, radius, Paints.Outline);
    }

    private (int Row, float InRow)? GetRowAt(Point position)
    {
        if (!Contains(position))
        {
            return null;
        }

        var rowHeight = RowHeight;

        var y = (float)position.Y - Top - OperatorsPadding - OutputRowsArea;

        var row = (int)Math.Floor(y / (rowHeight + OperatorRowGap));

        var inRow = y - row * (rowHeight + OperatorRowGap);

        return row < 0 || row >= Rows.Length || inRow >= rowHeight ? null : (row, inRow);
    }

    private int? GetRowIndex(ExecutionOperatorEvent operatorEvent)
    {
        for (var row = 0; row < Rows.Length; row++)
        {
            if (ReferenceEquals(Rows[row].Operator, operatorEvent))
            {
                return row;
            }
        }

        return null;
    }

    private void SelectOperator(TraceOperatorHit? hit)
    {
        var clicked = hit is { } found ? Rows[found.Row].Operator : null;

        SelectedOperator = ReferenceEquals(clicked, SelectedOperator) ? null : clicked;

        Host.OnSelectedOperatorChanged(SelectedOperator);
    }

    private string GetDetails(TraceOperatorHit hit)
    {
        var row = Rows[hit.Row];

        var lifetime = row.Lifetimes[hit.Track];

        if (Details.TryGetValue(lifetime, out var details))
        {
            return details;
        }

        var methods = new Dictionary<string, int>();

        var active = 0d;

        foreach (var call in lifetime.InstructionCalls)
        {
            var label = Host.GetLabel(call.Node);

            var separator = label.LastIndexOf("::", StringComparison.Ordinal);

            var method = separator < 0 ? label : label[(separator + 2)..];

            methods[method] = methods.GetValueOrDefault(method) + 1;

            active += call.End - call.Start;
        }

        var instructions = lifetime.EndOf(TraceTimelineAxis.Instructions) - lifetime.StartOf(TraceTimelineAxis.Instructions);

        var origin = Host.GetOrigin(TraceTimelineAxis.Position);

        var start = lifetime.StartOf(TraceTimelineAxis.Position) - origin;

        var end = lifetime.EndOf(TraceTimelineAxis.Position) - origin;

        var lines = new List<string>
        {
            row.Title,
            $"Thread {lifetime.Thread}",
            string.Join("  ·  ", methods.Select(m => $"{m.Key} {m.Value:N0}")),
            $"Instructions {instructions:N0}",
            $"Position {Math.Floor(start):N0} to {Math.Floor(end):N0}"
        };

        if (instructions > 0)
        {
            lines.Add($"Inside Calls {active / instructions:P1} (Including Child Operators)");
        }

        if (!lifetime.InUse.IsEmpty)
        {
            lines.Add($"Peak In Use {SizeFormat.Format((long)lifetime.InUse.PeakDuring(start, end))}");
        }

        details = string.Join(Environment.NewLine, lines);

        Details[lifetime] = details;

        return details;
    }
}
