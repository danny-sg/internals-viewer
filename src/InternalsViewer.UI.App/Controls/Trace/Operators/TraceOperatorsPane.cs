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

namespace InternalsViewer.UI.App.Controls.Trace.Operators;

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

    private const float StatementRowHeight = 34f;

    private const string ToolTipTitle = "Operators";

    private const string DefaultStatementName = "Statement";

    private const int StatementNodeId = -1;

    private static readonly SKColor StatementColour = new(130, 130, 130);

    private static readonly SKColor OperatorPaneColour = new(30, 30, 30, 220);

    private static readonly SKColor OperatorLabelColour = new(235, 235, 235);

    private static readonly SKColor OperatorSelectedColour = new(255, 255, 255, 230);

    private static readonly SKColor OperatorDimColour = new(0, 0, 0, 160);

    public TraceOperatorRow[] Rows { get; private set; } = [];

    public bool HasRows => Rows.Length > 0;

    public ExecutionOperatorEvent? SelectedOperator { get; private set; }

    public EventColourProvider? ColourProvider { get; set; }

    public string? StatementName
    {
        get;
        set
        {
            field = value;

            StatementEvent = null;
        }
    }

    public TraceOperatorsHover Hover { get; set; } = TraceOperatorsHover.None;

    private ITraceTimelineHost Host { get; } = host;

    private Func<(float Top, float Height)> GetBounds { get; } = getBounds;

    private TraceScale TraceScale { get; set; } = TraceScale.Default;

    private Dictionary<TraceOperatorLifetime, string> Details { get; } = new(ReferenceEqualityComparer.Instance);

    private Dictionary<SKColor, SKShader> Gradients { get; } = [];

    private SKRoundRect Shape { get; } = new();

    private TraceTimelinePaints Paints => Host.Paints;

    private float Top => GetBounds().Top;

    private float Height => GetBounds().Height;

    private TraceTimelineAxis Axis => Host.Axis;

    private ExecutionOperatorEvent? StatementEvent { get; set; }

    private RowLayout? CachedLayout { get; set; }

    private int LayoutVersion { get; set; }

    public void Rebuild(TraceTimeline? timeline, TraceOperatorLifetimes lifetimes)
    {
        TraceScale = timeline?.Scale ?? TraceScale.Default;

        Rows = AddStatementRow(GetOperatorRows(timeline, lifetimes));

        Details.Clear();

        Hover = TraceOperatorsHover.None;

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

        for (var row = 0; row < Rows.Length; row++)
        {
            for (var track = 0; track < Rows[row].Lifetimes.Length; track++)
            {
                if (GetLifespanRect(row, track, width, scale) is { } lifespan)
                {
                    DrawLifespan(canvas, row, track, lifespan);
                }

                if (GetTrackRect(row, track, width, scale) is { } rect)
                {
                    DrawOperatorBar(canvas, row, track, rect, scale);
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

        DrawTickHover(canvas, scale);

        DrawBandHover(canvas, scale);

        canvas.Restore();
    }

    public TraceOperatorsHover GetHover(Point position)
    {
        if (!Contains(position))
        {
            return TraceOperatorsHover.None;
        }

        var tick = GetTickHit(position);

        var operatorHit = tick is null ? GetOperatorAt(position) : null;

        return new TraceOperatorsHover(operatorHit, tick);
    }

    public string? GetToolTip(TraceOperatorsHover hover, Point position)
    {
        if (hover.Operator is { } operatorHit)
        {
            return GetDetails(operatorHit);
        }

        if (hover.Tick is { } tick)
        {
            return GetTickToolTip(tick);
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

        var track = Math.Clamp((int)(inRow / (GetRowHeight(row) / lifetimes.Length)), 0, lifetimes.Length - 1);

        var scale = Host.Scale;

        var value = Host.ViewStart + position.X / scale;

        var tolerance = TraceTimelineControl.HitTolerance / scale;

        return lifetimes[track].GetConstructed(Axis) - tolerance <= value && value <= lifetimes[track].GetDestroyed(Axis) + tolerance
            ? new TraceOperatorHit(row, track)
            : null;
    }

    public bool IsBackground(Point position) => GetOperatorAt(position) is null;

    public (double Start, double End) GetActiveRange(TraceOperatorHit hit)
    {
        var lifetime = Rows[hit.Row].Lifetimes[hit.Track];

        return (lifetime.GetStart(Axis), lifetime.GetEnd(Axis));
    }

    public PlanNodeIdentifier? GetPlanNode(Point position)
        => GetOperatorAt(position) is { } hit ? Rows[hit.Row].Operator.PlanNodeIdentifier : null;

    public void Click(Point position)
    {
        if (GetTickAt(position) is { } tick)
        {
            Host.ClickCall(tick.Node, tick.CallIndex);
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

    private static TraceOperatorRow[] GetOperatorRows(TraceTimeline? timeline, TraceOperatorLifetimes lifetimes)
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

        return [.. lifetimes.Where(l => lanes.ContainsKey(l.Thread))
                            .GroupBy(l => l.Operator, ReferenceEqualityComparer.Instance)
                            .Select(g => CreateRow((ExecutionOperatorEvent)g.Key!, [.. g.OrderBy(l => lanes[l.Thread])]))
                            .OrderBy(r => r.Operator.NodeLevel)
                            .ThenBy(r => r.Lifetimes.Min(l => l.Start.Position))
                            .ThenBy(r => r.Operator.PlanNodeIdentifier?.NodeId ?? 0)];
    }

    private TraceOperatorRow[] AddStatementRow(TraceOperatorRow[] rows)
    {
        if (rows.Length == 0)
        {
            return rows;
        }

        var root = rows[0];

        var statement = GetStatementEvent(root.Operator);

        var row = new TraceOperatorRow(statement, root.Lifetimes, statement.Name, string.Empty, statement.OperatorDescription)
        {
            TickOperator = root.Operator,
            IsStatement = true
        };

        return [row, .. rows];
    }

    private ExecutionOperatorEvent GetStatementEvent(ExecutionOperatorEvent root)
    {
        if (StatementEvent is { } existing)
        {
            return existing;
        }

        var name = StatementName ?? DefaultStatementName;

        StatementEvent = new ExecutionOperatorEvent
        {
            Name = name,
            OperatorDescription = name,
            PlanNodeIdentifier = root.PlanNodeIdentifier is { } identifier ? new PlanNodeIdentifier(identifier.PlanHandleId, StatementNodeId) : null
        };

        return StatementEvent;
    }

    private static TraceOperatorRow CreateRow(ExecutionOperatorEvent operatorEvent, TraceOperatorLifetime[] lifetimes)
    {
        var target = operatorEvent.ObjectName.Length > 0
            ? operatorEvent.ObjectName
            : operatorEvent.LogicalOperator != operatorEvent.Name ? operatorEvent.LogicalOperator : string.Empty;

        return new TraceOperatorRow(operatorEvent, lifetimes, operatorEvent.Name, target, operatorEvent.Label);
    }

    private void DrawOperatorBar(SKCanvas canvas, int row, int track, SKRect rect, double scale)
    {
        var colour = GetOperatorColour(Rows[row].Operator, Rows[row].Lifetimes[track]);

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

        var label = DrawStrips(canvas, row, track, rect, colour, scale);

        DrawOperatorLabel(canvas, Rows[row], label);
    }

    private void DrawLifespan(SKCanvas canvas, int row, int track, SKRect rect)
    {
        var colour = GetOperatorColour(Rows[row].Operator, Rows[row].Lifetimes[track]);

        var radius = Math.Min(rect.Height / 2, OperatorCornerRadius);

        Shape.SetRect(rect, radius, radius);

        canvas.Save();

        canvas.ClipRoundRect(Shape, antialias: true);

        Paints.Fill.Color = colour.WithAlpha(LifespanAlpha);

        canvas.DrawRect(rect, Paints.Fill);

        DrawStripShades(canvas, row, track, rect, colour);

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

        return GetSpanRect(row, track, lifetime.GetStart(Axis), lifetime.GetEnd(Axis), width, scale);
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
        var trackHeight = GetRowHeight(row) / Rows[row].Lifetimes.Length;

        var padding = Math.Min(OperatorBarPadding, trackHeight * OperatorBarShare);

        return (GetRowTop(row) + track * trackHeight + padding, Math.Max(trackHeight - padding * 2, 1));
    }

    private RowLayout GetLayout()
    {
        var (top, height) = GetBounds();

        if (CachedLayout is { } layout && layout.Matches(top, height, Rows, LayoutVersion))
        {
            return layout;
        }

        CachedLayout = BuildRowLayout(top, height);

        return CachedLayout;
    }

    private RowLayout BuildRowLayout(float top, float height)
    {
        var tops = new float[Rows.Length];

        var heights = new float[Rows.Length];

        var available = height - OperatorsPadding * 2 - OperatorsBottomGap - Math.Max(Rows.Length - 1, 0) * OperatorRowGap;

        var fixedHeight = 0f;

        var flexible = 0;

        foreach (var row in Rows)
        {
            if (GetFixedHeight(row) is { } value)
            {
                fixedHeight += value;
            }
            else
            {
                flexible++;
            }
        }

        var shared = flexible == 0 ? 0 : Math.Max((available - fixedHeight) / flexible, MinimumRowHeight);

        var rowTop = top + OperatorsPadding;

        for (var row = 0; row < Rows.Length; row++)
        {
            heights[row] = GetFixedHeight(Rows[row]) ?? shared;

            tops[row] = rowTop;

            rowTop += heights[row] + OperatorRowGap;
        }

        return new RowLayout(top, height, Rows, LayoutVersion, tops, heights, shared);
    }

    private float? GetFixedHeight(TraceOperatorRow row)
        => RowHeights.TryGetValue(row.Operator, out var height) ? height : row.IsStatement ? StatementRowHeight : null;

    private float GetRowTop(int row) => GetLayout().Tops[row];

    private float GetRowHeight(int row) => GetLayout().Heights[row];

    private SKColor GetOperatorColour(ExecutionOperatorEvent operatorEvent, TraceOperatorLifetime lifetime)
        => IsStatement(operatorEvent)
            ? StatementColour
            : ColourProvider is { } colours
                ? colours.GetColour(operatorEvent).ToSkColor()
                : Host.GetColour(lifetime.CallNodeIndex);

    private bool IsStatement(ExecutionOperatorEvent operatorEvent)
        => Rows.Length > 0 && Rows[0].IsStatement && ReferenceEquals(Rows[0].Operator, operatorEvent);

    private void DimOperators(SKCanvas canvas, int width)
    {
        if (SelectedOperator is null || Host.ShowCallStack)
        {
            return;
        }

        for (var row = 0; row < Rows.Length; row++)
        {
            if (ReferenceEquals(Rows[row].Operator, SelectedOperator))
            {
                var top = GetRowTop(row);

                Host.PathBuilder.AddRect(new SKRect(0, top, width, top + GetRowHeight(row)));
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

        var layout = GetLayout();

        var y = (float)position.Y;

        for (var row = 0; row < Rows.Length; row++)
        {
            if (y >= layout.Tops[row] && y < layout.Tops[row] + layout.Heights[row])
            {
                return (row, y - layout.Tops[row]);
            }
        }

        return null;
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
        if (hit is { } statement && Rows[statement.Row].IsStatement)
        {
            return;
        }

        var clicked = hit is { } found ? Rows[found.Row].Operator : null;

        SelectedOperator = ReferenceEquals(clicked, SelectedOperator) ? null : clicked;

        Host.OnSelectedOperatorChanged(SelectedOperator);
    }

    private string GetDetails(TraceOperatorHit hit)
    {
        var row = Rows[hit.Row];

        if (row.IsStatement)
        {
            return row.Title;
        }

        var lifetime = row.Lifetimes[hit.Track];

        if (Details.TryGetValue(lifetime, out var details))
        {
            return details;
        }

        var methods = new Dictionary<string, int>();

        var active = 0ul;

        foreach (var call in lifetime.Calls)
        {
            var label = Host.GetLabel(call.CallNodeIndex);

            var separator = label.LastIndexOf("::", StringComparison.Ordinal);

            var method = separator < 0 ? label : label[(separator + 2)..];

            methods[method] = methods.GetValueOrDefault(method) + 1;

            active += call.Instructions;
        }

        var instructions = lifetime.End.Instructions - lifetime.Start.Instructions;

        var origin = Host.GetOrigin(TraceTimelineAxis.Position);

        var start = lifetime.GetStart(TraceTimelineAxis.Position) - origin;

        var end = lifetime.GetEnd(TraceTimelineAxis.Position) - origin;

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
            lines.Add($"Inside Calls {(double)active / instructions:P1} (Including Child Operators)");
        }

        if (!lifetime.InUse.IsEmpty)
        {
            var peak = lifetime.InUse.PeakDuring(lifetime.Start.Position, lifetime.End.Position);

            lines.Add($"Peak In Use {SizeFormat.Format((long)peak)}");
        }

        details = string.Join(Environment.NewLine, lines);

        Details[lifetime] = details;

        return details;
    }

    private sealed record RowLayout(float Top,
                                    float Height,
                                    TraceOperatorRow[] Rows,
                                    int Version,
                                    float[] Tops,
                                    float[] Heights,
                                    float Shared)
    {
        public bool Matches(float top, float height, TraceOperatorRow[] rows, int version)
            => Top == top && Height == height && ReferenceEquals(Rows, rows) && Version == version;
    }
}
