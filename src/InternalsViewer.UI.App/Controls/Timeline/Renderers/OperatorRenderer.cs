using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.UI.App.Helpers;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline.Renderers;

/// <summary>
/// Draws the operator bars from the laid-out <see cref="OperatorBar"/> list: the gradient bar, its worker-thread
/// overlay, consume-phase shade, object-colour marker and label, plus the selected operator's row-flow path
/// </summary>
/// <remarks>
/// Owns the operator-specific paints and fonts (gradient bar, label text, flow connector, outline); plain fills come
/// from the shared palette. Consumes the bars the control builds so the trace rails and the bars share one layout.
/// </remarks>
internal sealed class OperatorRenderer(RenderResource resources, CurrentSelection selection, List<HitRegion> hitRegions)
    : IDisposable
{
    // Top-lit sheen: the bar's top edge is lightened and its bottom darkened by this fraction.
    private const float GradientLift = 0.04f;

    // A bar only gets a label when it is at least this tall and wide.
    private const float MinLabelBarHeight = 11f;
    private const float MinLabelBarWidth = 26f;

    // Below this per-worker lane height the thread overlay falls back to a concurrency-density shade.
    private const float MinThreadLaneHeight = 2.5f;
    private const float ThreadLaneGap = 1f;

    private const float ObjectMarkerMargin = 3f;
    private const float ObjectMarkerRadius = 6f;
    private const float ObjectMarkerBandWidth = 12f;

    private const float OperatorMaxFont = 12f;
    private const float OperatorMinFont = 7f;
    private const float TwoLineGap = 2f;
    private const float OperatorLabelGapFraction = 0.5f;

    private static readonly SKColor ConsumeShadeColour = new(0, 0, 0, 115);
    private static readonly SKColor OperatorLabelColour = new(235, 235, 235);
    private static readonly SKColor FlowConnectorColour = new(120, 200, 255, 70);
    private static readonly SKColor FlowPathColour = new(200, 200, 200, 200);
    private static readonly SKColor FlowSelectedColour = new(255, 255, 255, 230);

    private readonly SKPaint _bar = new() { Color = SKColors.LimeGreen, Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _text = new() { IsAntialias = true };
    private readonly SKPaint _flowConnector = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _outline = new() { Style = SKPaintStyle.Stroke, IsAntialias = true };

    private static readonly SKTypeface BoldTypeface = SKTypeface.FromFamilyName(SKTypeface.Default.FamilyName, SKFontStyle.Bold);

    private readonly SKFont _font = new(SKTypeface.Default, 12f);
    private readonly SKFont _boldFont = new(BoldTypeface, 10f);

    public void Draw(SKCanvas canvas, TimelineFrame frame, IReadOnlyList<OperatorBar> bars)
    {
        var rightEdge = frame.CanvasWidth;

        foreach (var b in bars)
        {
            if (b.EndX < frame.RowLabelWidth || b.StartX > rightEdge)
            {
                continue;
            }

            // Subtle top-lit sheen: lighten the top edge, darken the bottom edge of the bar
            var gradient = SKShader.CreateLinearGradient(new SKPoint(b.StartX, b.BarTop),
                                                         new SKPoint(b.StartX, b.BarBottom),
                                                         [
                                                             TimelineColours.Scale(b.BarColour, 1f + GradientLift),
                                                             TimelineColours.Scale(b.BarColour, 1f - GradientLift)
                                                         ],
                                                         null,
                                                         SKShaderTileMode.Clamp);

            _bar.Color = b.BarColour;
            _bar.Shader = gradient;

            canvas.DrawRoundRect(new SKRect(b.StartX, b.BarTop, b.EndX, b.BarBottom),
                                 b.CornerRadius, b.CornerRadius, _bar);

            _bar.Shader = null;

            gradient.Dispose();

            if (frame.ShowThreads && b.Op.Threads.Count > 1)
            {
                DrawThreads(canvas, frame, b);
            }

            DrawConsumeShade(canvas, frame, b);

            if (b.LineWidth >= MinLabelBarHeight && b.EndX - b.StartX >= MinLabelBarWidth)
            {
                DrawLabel(canvas, b.Op, b.StartX + 4, b.EndX - 4, b.BarCentreY, b.LineWidth);
            }

            DrawObjectColourMarker(canvas, frame, b);

            hitRegions.Add(new HitRegion(new SKRect(b.StartX, b.SlotCentreY - b.SlotHeight / 2f, b.EndX,
                                                    b.SlotCentreY + b.SlotHeight / 2f), b.Op, null));
        }

        if (selection.NodeId is { } selected)
        {
            DrawRowFlowPath(canvas, frame, bars, selected);
        }
    }

    /// <remarks>
    /// Overlays a parallel operator's worker threads on its bar. The coordinator (thread 0) is the bar itself, so only
    /// the workers (non-zero ids) get sub-lanes: each spans its own start→end (time skew) and is as tall as its share of
    /// the rows processed (data skew). When the lanes would be too thin to read, falls back to a concurrency-density fill.
    /// </remarks>
    private void DrawThreads(SKCanvas canvas, TimelineFrame frame, OperatorBar b)
    {
        var workers = b.Op.Threads.Where(t => t.ThreadId != 0).ToList();

        if (workers.Count == 0)
        {
            return;
        }

        var barHeight = b.BarBottom - b.BarTop;

        if (barHeight / workers.Count < MinThreadLaneHeight)
        {
            DrawThreadDensity(canvas, frame, b, workers);
            return;
        }

        // Stack the workers, each lane as tall as its share of the rows (so an over-loaded thread reads as a thick lane
        // and an idle one as a sliver). Fall back to equal shares with no row counts.
        var totalRows = workers.Sum(t => t.RowsProcessed);

        var y = b.BarTop;

        foreach (var t in workers)
        {
            var share = totalRows > 0 ? (float)t.RowsProcessed / totalRows : 1f / workers.Count;
            var laneHeight = barHeight * share;

            if (laneHeight >= 0.5f)
            {
                var x0 = Math.Max(b.StartX, frame.TimeToX(t.StartUs / frame.AxisUnitsPerMs));
                var x1 = Math.Min(b.EndX, frame.TimeToX(t.EndUs / frame.AxisUnitsPerMs));

                if (x1 < x0 + 1f)
                {
                    x1 = x0 + 1f;
                }

                // Workers read a touch brighter than the envelope (coordinator) bar behind them.
                resources.Fill.Color = TimelineColours.Scale(b.BarColour, 1.12f);

                canvas.DrawRect(x0, y, x1 - x0, Math.Max(1f, laneHeight - ThreadLaneGap), resources.Fill);
            }

            y += laneHeight;
        }
    }

    // Shades the envelope bar by the number of worker threads running concurrently over time (darker = more overlap),
    // by sweeping their start/end points.
    private void DrawThreadDensity(SKCanvas canvas, TimelineFrame frame, OperatorBar b, List<OperatorThread> workers)
    {
        var points = new List<(double Ms, int Delta)>(workers.Count * 2);

        foreach (var t in workers)
        {
            points.Add((t.StartUs / frame.AxisUnitsPerMs, +1));
            points.Add((t.EndUs / frame.AxisUnitsPerMs, -1));
        }

        points.Sort((p, q) => p.Ms.CompareTo(q.Ms));

        var active = 0;
        var previousMs = points[0].Ms;

        foreach (var (ms, delta) in points)
        {
            if (ms > previousMs && active > 0)
            {
                var x0 = Math.Max(b.StartX, frame.TimeToX(previousMs));
                var x1 = Math.Min(b.EndX, frame.TimeToX(ms));

                if (x1 > x0)
                {
                    // Map 1..DOP concurrent workers onto a 0.7 → 1.3 brightness ramp over the envelope.
                    var intensity = (float)active / workers.Count;

                    resources.Fill.Color = TimelineColours.Scale(b.BarColour, 0.7f + 0.6f * intensity);

                    canvas.DrawRect(x0, b.BarTop, x1 - x0, b.BarBottom - b.BarTop, resources.Fill);
                }
            }

            active += delta;

            previousMs = ms;
        }
    }

    // Dims the consume (build) phase of a blocking operator on its bar: the span where it is reading its input but has
    // not yet started emitting rows to its parent. The undimmed remainder of the bar is the emit phase.
    private void DrawConsumeShade(SKCanvas canvas, TimelineFrame frame, OperatorBar b)
    {
        if (b.Op.BuildPhaseDurationUs <= 0)
        {
            return;
        }

        var consumeStartX = Math.Max(b.StartX, frame.TimeToX(b.Op.BuildPhaseTimeUs / frame.AxisUnitsPerMs));
        var consumeEndX = Math.Min(b.EndX,
            frame.TimeToX((b.Op.BuildPhaseTimeUs + b.Op.BuildPhaseDurationUs) / frame.AxisUnitsPerMs));

        if (consumeEndX <= consumeStartX)
        {
            return;
        }

        // Clip to the bar so the overlay respects its rounded corners. SKRoundRect wraps a native handle, so dispose it
        // once the clip is applied — the canvas has already copied it into its clip stack.
        canvas.Save();

        using (var clip = new SKRoundRect(new SKRect(b.StartX, b.BarTop, b.EndX, b.BarBottom),
                                          b.CornerRadius, b.CornerRadius))
        {
            canvas.ClipRoundRect(clip, antialias: true);
        }

        resources.Fill.Color = ConsumeShadeColour;
        canvas.DrawRect(consumeStartX, b.BarTop, consumeEndX - consumeStartX, b.BarBottom - b.BarTop, resources.Fill);

        canvas.Restore();

        hitRegions.Add(new HitRegion(new SKRect(consumeStartX, b.BarTop, consumeEndX, b.BarBottom), b.Op, "Consuming"));
    }

    private void DrawObjectColourMarker(SKCanvas canvas, TimelineFrame frame, OperatorBar b)
    {
        if (frame.ColourProvider?.GetObjectColour(b.Op.ObjectName) is not { } colour)
        {
            return;
        }

        if (b.BarBottom - b.BarTop < ObjectMarkerMargin + 2 * ObjectMarkerRadius)
        {
            var bandWidth = Math.Min(ObjectMarkerBandWidth, b.EndX - b.StartX);

            if (bandWidth <= 0)
            {
                return;
            }

            canvas.Save();

            using (var clip = new SKRoundRect(new SKRect(b.StartX, b.BarTop, b.EndX, b.BarBottom),
                                              b.CornerRadius, b.CornerRadius))
            {
                canvas.ClipRoundRect(clip, antialias: true);
            }

            resources.Fill.Color = colour.ToSkColor();

            canvas.DrawRect(b.StartX, b.BarTop, bandWidth, b.BarBottom - b.BarTop, resources.Fill);

            canvas.Restore();

            return;
        }

        // Otherwise a small object-colour dot in the operator bar's top-left corner (3px inset, 4px across).
        var centreX = b.StartX + ObjectMarkerMargin + ObjectMarkerRadius;
        var centreY = b.BarTop + ObjectMarkerMargin + ObjectMarkerRadius;

        // Skip if the bar is too narrow to hold the dot inside its corner.
        if (centreX + ObjectMarkerRadius > b.EndX)
        {
            return;
        }

        // The dot is antialiased; reset the shared fill afterwards so other callers get the crisp default.
        resources.Fill.IsAntialias = true;
        resources.Fill.Color = colour.ToSkColor();

        canvas.DrawCircle(centreX, centreY, ObjectMarkerRadius, resources.Fill);

        resources.Fill.IsAntialias = false;
    }

    /// <summary>
    /// Traces the clicked operator's rows up to the root: a connector for each child→parent hop, lit only over the
    /// window the source is emitting. Because emit time only moves later up the tree, the lit segments form a rising
    /// staircase showing where the flow is held up.
    /// </summary>
    private void DrawRowFlowPath(SKCanvas canvas, TimelineFrame frame, IReadOnlyList<OperatorBar> bars, int selectedNodeId)
    {
        var barByNode = new Dictionary<int, OperatorBar>(bars.Count);

        foreach (var bar in bars)
        {
            if (bar.Op.PlanNodeIdentifier is { } id)
            {
                barByNode[id.NodeId] = bar;
            }
        }

        if (!barByNode.TryGetValue(selectedNodeId, out var start))
        {
            return;
        }

        // The chain selected → … → root
        var chain = new List<OperatorBar> { start };

        var current = start;

        while (current.Op.ParentNodeId is { } parentId && barByNode.TryGetValue(parentId, out var parent))
        {
            chain.Add(parent);
            current = parent;
        }

        // Connector ribbons between consecutive operators, lit over the child's emit window.
        for (var i = 0; i < chain.Count - 1; i++)
        {
            DrawFlowConnector(canvas, frame, chain[i], chain[i + 1]);
        }

        // Outline each operator on the path; the clicked one stands out.
        foreach (var bar in chain)
        {
            var isSelected = bar.Op.PlanNodeIdentifier?.NodeId == selectedNodeId;
            OutlineBar(canvas, bar, isSelected ? FlowSelectedColour : FlowPathColour, isSelected ? 2f : 1f);
        }
    }

    private void DrawFlowConnector(SKCanvas canvas, TimelineFrame frame, OperatorBar child, OperatorBar parent)
    {
        // Rows flow from the child while it is emitting: [EmitStart, End].
        var x0 = Math.Max(frame.RowLabelWidth, frame.TimeToX(child.Op.EmitStartUs / frame.AxisUnitsPerMs));

        var x1 = Math.Min(frame.CanvasWidth, frame.TimeToX((child.Op.TimeUs + child.Op.DurationUs) / frame.AxisUnitsPerMs));

        if (x1 <= x0)
        {
            return;
        }

        // Bridge the two bars from the top edge of the upper to the bottom edge of the lower.
        var yLo = Math.Min(child.BarTop, parent.BarTop);

        var yHi = Math.Max(child.BarBottom, parent.BarBottom);

        _flowConnector.Color = FlowConnectorColour;

        canvas.DrawRect(x0, yLo, x1 - x0, yHi - yLo, _flowConnector);
    }

    private void OutlineBar(SKCanvas canvas, OperatorBar b, SKColor colour, float strokeWidth)
    {
        _outline.Color = colour;
        _outline.StrokeWidth = strokeWidth;

        canvas.DrawRoundRect(new SKRect(b.StartX, b.BarTop, b.EndX, b.BarBottom),
                             b.CornerRadius,
                             b.CornerRadius,
                             _outline);
    }

    /// <summary>
    /// Draws the operator label, choosing a layout by available width and height: two lines (name over object), one line
    /// (name + object), one line (name alone), or none
    /// </summary>
    private void DrawLabel(SKCanvas canvas,
                           ExecutionOperatorEvent planOperator,
                           float startX,
                           float endX,
                           float y,
                           float barHeight)
    {
        var opName = planOperator.Name;

        // Operators with no object of their own (joins, sorts, ...) show their logical operator on the second line.
        var target = planOperator.ObjectName.Length > 0
                     ? planOperator.ObjectName
                     : planOperator.LogicalOperator != planOperator.Name ? planOperator.LogicalOperator : string.Empty;

        var dop = planOperator.Threads.Count(t => t.ThreadId != 0);

        Span<char> charBuffer = stackalloc char[64];

        var typeSpan = BuildTypeSpan(opName, dop, charBuffer);

        if (typeSpan.Length == 0 && target.Length == 0)
        {
            return;
        }

        const float textPadX = 14f;

        var availableWidth = endX - startX - textPadX * 2;

        if (availableWidth <= 0)
        {
            return;
        }

        // Widths scale linearly with font size, so measure once at the cap and derive each layout's width-bound size.
        _boldFont.Size = OperatorMaxFont;
        _font.Size = OperatorMaxFont;

        var typeWidth = typeSpan.Length > 0 ? _boldFont.MeasureText(typeSpan) : 0f;
        var targetWidth = target.Length > 0 ? _font.MeasureText((ReadOnlySpan<char>)target) : 0f;

        var hasBoth = typeSpan.Length > 0 && target.Length > 0;

        _text.Color = OperatorLabelColour;

        // 1. Two lines: type above the object name, left aligned.
        if (hasBoth)
        {
            var widerAtMax = Math.Max(typeWidth, targetWidth);

            var sizeByWidth = widerAtMax <= 0 ? OperatorMaxFont : OperatorMaxFont * availableWidth / widerAtMax;

            var sizeByHeight = (barHeight - 2f - TwoLineGap) / 2f;

            var size = Math.Min(OperatorMaxFont, Math.Min(sizeByWidth, sizeByHeight));

            if (size >= OperatorMinFont)
            {
                _boldFont.Size = size;
                _font.Size = size;

                var x = startX + textPadX;
                var halfGap = (size + TwoLineGap) / 2f;

                DrawTextSpan(canvas, typeSpan, x, y - halfGap + size * 0.35f, _boldFont, _text);
                DrawTextSpan(canvas, target, x, y + halfGap + size * 0.35f, _font, _text);
                return;
            }
        }

        // 2. Single line: type then object name, left aligned.
        if (hasBoth)
        {
            var widthAtMax = typeWidth + targetWidth + OperatorMaxFont * OperatorLabelGapFraction;

            var sizeByWidth = widthAtMax <= 0 ? OperatorMaxFont : OperatorMaxFont * availableWidth / widthAtMax;

            var sizeByHeight = barHeight - 2f;

            var size = Math.Min(OperatorMaxFont, Math.Min(sizeByWidth, sizeByHeight));

            if (size >= OperatorMinFont)
            {
                _boldFont.Size = size;
                _font.Size = size;

                var baseline = y + size * 0.35f;
                var x = startX + textPadX;

                DrawTextSpan(canvas, typeSpan, x, baseline, _boldFont, _text);

                x += _boldFont.MeasureText(typeSpan, null) + size * OperatorLabelGapFraction;

                DrawTextSpan(canvas, target, x, baseline, _font, _text);

                return;
            }
        }

        // 3. Single line: the operator type alone (or whichever single label is present).
        var isPrimaryType = typeSpan.Length > 0;
        var primarySpan = isPrimaryType ? typeSpan : (ReadOnlySpan<char>)target;
        var primaryFont = isPrimaryType ? _boldFont : _font;
        var primaryWAtMax = isPrimaryType ? typeWidth : targetWidth;

        var nameSizeByWidth = primaryWAtMax <= 0 ? OperatorMaxFont : OperatorMaxFont * availableWidth / primaryWAtMax;
        var nameSize = Math.Min(OperatorMaxFont, Math.Min(nameSizeByWidth, barHeight - 2f));

        if (nameSize >= OperatorMinFont)
        {
            primaryFont.Size = nameSize;
            DrawTextSpan(canvas, primarySpan, startX + textPadX, y + nameSize * 0.35f, primaryFont, _text);
        }
    }

    private static ReadOnlySpan<char> BuildTypeSpan(string operatorName, int dop, Span<char> charBuffer)
    {
        if (operatorName.Length == 0)
        {
            return ReadOnlySpan<char>.Empty;
        }

        operatorName.AsSpan().CopyTo(charBuffer);

        var position = operatorName.Length;

        if (dop > 1 && position + 4 <= charBuffer.Length)
        {
            charBuffer[position++] = ' ';
            charBuffer[position++] = '×';

            var ok = dop.TryFormat(charBuffer[position..], out var written);

            position += ok ? written : 0;
        }

        return charBuffer[..position];
    }

    private static void DrawTextSpan(SKCanvas canvas,
                                     ReadOnlySpan<char> text,
                                     float x,
                                     float y,
                                     SKFont font,
                                     SKPaint paint)
    {
        using var blob = SKTextBlob.Create(text, font, SKPoint.Empty);

        if (blob is not null)
        {
            canvas.DrawText(blob, x, y, paint);
        }
    }

    public void Dispose()
    {
        _bar.Dispose();
        _text.Dispose();
        _flowConnector.Dispose();
        _outline.Dispose();
        _font.Dispose();
        _boldFont.Dispose();
    }
}
