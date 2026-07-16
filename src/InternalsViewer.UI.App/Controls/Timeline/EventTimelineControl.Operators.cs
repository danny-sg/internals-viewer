using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Parsing.Plans;
using InternalsViewer.UI.App.Helpers;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline;

public sealed partial class EventTimelineControl
{
    private readonly record struct OperatorBar(ExecutionOperatorEvent Op,
                                               float StartX,
                                               float EndX,
                                               float BarTop,
                                               float BarBottom,
                                               float BarCentreY,
                                               float LineWidth,
                                               float CornerRadius,
                                               float SlotCentreY,
                                               float SlotHeight,
                                               SKColor BarColour);

    private void DrawOperatorLines(SKCanvas canvas, float[] rowTops, float[] rowHeights)
    {
        var rows = _rows.Active;

        var planRow = -1;

        for (var r = 0; r < rows.Count; r++)
        {
            if (rows[r].EventType == typeof(ExecutionOperatorEvent))
            {
                planRow = r; break;
            }
        }

        if (planRow < 0)
        {
            return;
        }

        var ordered = _orderedOperators;

        if (ordered.Count == 0)
        {
            return;
        }

        var maxCost = _maxCost;
        var maxRows = _maxRows;

        var top = rowTops[planRow] + RowPadding;
        var height = rowHeights[planRow] - RowPadding * 2;

        var weights = new float[ordered.Count];

        for (var i = 0; i < ordered.Count; i++)
        {
            weights[i] = CostWeight(ordered[i].Op);
        }

        var totalWeight = weights.Sum();

        var slotHeights = OperatorSlotLayout.Resolve(weights, totalWeight, height);

        var slotByIndex = new Dictionary<int, (float Y, float Height)>(ordered.Count);
        var slotAcc = top;

        for (var i = 0; i < ordered.Count; i++)
        {
            var slot = slotHeights[i];

            slotByIndex[ordered[i].Index] = (slotAcc + slot / 2f, slot);
            slotAcc += slot;
        }

        var bars = new List<OperatorBar>(ordered.Count);

        foreach (var (index, op) in ordered)
        {
            var startX = TimeToX(_times[index]);
            var endX = TimeToX(_times[index] + DurationMs(op));
            if (endX < startX + 2)
            {
                endX = startX + 2;
            }

            // Pad the right edge so an I/O event landing on the operator's end time (its marker drawn
            // rightward from endX) still falls within the bar, allowing for the wider sparse-row marker.
            endX += SparseMarkerWidth;

            var level = op.NodeLevel;
            var (y, slotHeight) = slotByIndex[index];

            SKColor barColour;

            if (level == 0)
            {
                // The statement (SELECT) node is a single grey bar (a half-height slot in the stack).
                barColour = StatementColour;
            }
            else
            {
                // Fall back to the row colour when there's no colour provider yet.
                barColour = ColourProvider is { } colours
                    ? colours.GetColour(op).ToSkColor()
                    : rows[planRow].Color;
            }

            // Lay the bar out within the slot. Buffer operators collapse to a thin bar; everything else
            // fills the slot less a margin.
            var slotTop = y - slotHeight / 2f;
            var slotBottom = y + slotHeight / 2f;

            // In Trace mode add extra padding so stacked bars leave a gap for the trace lines to show.
            var effectiveMargin = OperatorLineMargin + TraceStackGap;

            var pad = effectiveMargin / 2f;

            var availTop = slotTop + pad;
            var availBottom = Math.Max(availTop + 1f, slotBottom - pad);

            float barTop, barBottom;

            if (op.Category == OperatorCategory.Buffer)
            {
                // Collapse buffer operators (spool/sort/exchange) to a thin bar centred in the band.
                var barHeight = Math.Max(1f, (slotHeight - effectiveMargin) * BufferHeightScale);
                var centre = (availTop + availBottom) / 2f;
                barTop = centre - barHeight / 2f;
                barBottom = centre + barHeight / 2f;
            }
            else if (op.Category == OperatorCategory.DataAccess && maxRows > 0)
            {
                // Size scan/seek bars by rows processed: thicker = more data, sqrt-compressed against the
                // busiest data-access operator, with a floor so even a tiny scan stays visible.
                var available = availBottom - availTop;
                var fill = op.RowsProcessed > 0
                    ? Math.Clamp((float)Math.Sqrt(op.RowsProcessed / (double)maxRows), DataAccessMinFill, 1f)
                    : DataAccessMinFill;
                var barHeight = Math.Max(1f, available * fill);
                var centre = (availTop + availBottom) / 2f;
                barTop = centre - barHeight / 2f;
                barBottom = centre + barHeight / 2f;
            }
            else
            {
                barTop = availTop;
                barBottom = availBottom;
            }

            var lineWidth = Math.Max(1f, barBottom - barTop);
            var barCentreY = (barTop + barBottom) / 2f;
            var cornerRadius = Math.Min(lineWidth / 2f, 3f);

            bars.Add(new OperatorBar(op, startX, endX, barTop, barBottom, barCentreY,
                                     lineWidth, cornerRadius, y, slotHeight, barColour));
        }

        // Trace: draw the extensions first so the operator bars paint over them (always on).
        DrawTraces(canvas, bars, rowTops, rowHeights);

        var rightEdge = CanvasWidth;

        foreach (var b in bars)
        {
            if (b.EndX < RowLabelWidth || b.StartX > rightEdge)
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

            _operatorPaint.Color = b.BarColour;
            _operatorPaint.Shader = gradient;

            canvas.DrawRoundRect(new SKRect(b.StartX, b.BarTop, b.EndX, b.BarBottom),
                                 b.CornerRadius, b.CornerRadius, _operatorPaint);

            _operatorPaint.Shader = null;

            gradient.Dispose();

            if (_showThreads && b.Op.Threads.Count > 1)
            {
                DrawOperatorThreads(canvas, b);
            }

            DrawConsumeShade(canvas, b);

            if (b.LineWidth >= MinLabelBarHeight && b.EndX - b.StartX >= MinLabelBarWidth)
            {
                DrawOperatorLabel(canvas, b.Op, b.StartX + 4, b.EndX - 4, b.BarCentreY, b.LineWidth);
            }

            DrawObjectColourMarker(canvas, b);

            _hitRegions.Add((new SKRect(b.StartX, b.SlotCentreY - b.SlotHeight / 2f, b.EndX,
                                        b.SlotCentreY + b.SlotHeight / 2f), b.Op, null));
        }

        if (_selectedNodeId is { } selected)
        {
            DrawRowFlowPath(canvas, bars, selected);
        }

        return;

        float CostWeight(ExecutionOperatorEvent op)
        {
            if (op.NodeLevel == 0)
            {
                return StatementBandWeight;
            }

            if (maxCost <= 0)
            {
                // No cost information - fall back to an equal share for every operator
                return MaxCostWeight;
            }

            var normalised = (float)Math.Sqrt(Math.Clamp((op.Cost ?? 0) / maxCost, 0, 1));
            return MinCostWeight + (MaxCostWeight - MinCostWeight) * normalised;
        }
    }

    /// <remarks>
    /// Overlays a parallel operator's worker threads on its bar. The coordinator (thread 0) is the bar itself (its span is the whole
    /// block), so only the workers (non-zero ids) get sub-lanes: each spans its own start→end (time skew) and is as tall as its share of
    /// the rows processed (data skew). When the lanes would be too thin to read, falls back to a concurrency-density fill.
    /// </remarks>
    private void DrawOperatorThreads(SKCanvas canvas, OperatorBar b)
    {
        var workers = b.Op.Threads.Where(t => t.ThreadId != 0).ToList();
        if (workers.Count == 0)
        {
            return;
        }

        var barHeight = b.BarBottom - b.BarTop;

        if (barHeight / workers.Count < MinThreadLaneHeight)
        {
            DrawThreadDensity(canvas, b, workers);
            return;
        }

        // Stack the workers, each lane as tall as its share of the rows (so an over-loaded thread reads as a thick lane and an idle one
        // as a sliver). Fall back to equal shares with no row counts.
        var totalRows = workers.Sum(t => t.RowsProcessed);

        var y = b.BarTop;

        foreach (var t in workers)
        {
            var share = totalRows > 0 ? (float)t.RowsProcessed / totalRows : 1f / workers.Count;
            var laneHeight = barHeight * share;

            if (laneHeight >= 0.5f)
            {
                var x0 = Math.Max(b.StartX, TimeToX(t.StartUs / AxisUnitsPerMs));
                var x1 = Math.Min(b.EndX, TimeToX(t.EndUs / AxisUnitsPerMs));

                if (x1 < x0 + 1f)
                {
                    x1 = x0 + 1f;
                }

                // Workers read a touch brighter than the envelope (coordinator) bar behind them.
                _markerPaint.Color = TimelineColours.Scale(b.BarColour, 1.12f);

                canvas.DrawRect(x0, y, x1 - x0, Math.Max(1f, laneHeight - ThreadLaneGap), _markerPaint);
            }

            y += laneHeight;
        }
    }

    /// <summary>
    /// High degree-of-parallelism fallback
    /// </summary>
    /// <remarks>
    /// Shades the envelope bar by the number of worker threads running concurrently over time (darker = more overlap), by sweeping their
    /// start/end points.
    /// </remarks>
    private void DrawThreadDensity(SKCanvas canvas, OperatorBar b, List<OperatorThread> workers)
    {
        var points = new List<(double Ms, int Delta)>(workers.Count * 2);

        foreach (var t in workers)
        {
            points.Add((t.StartUs / AxisUnitsPerMs, +1));
            points.Add((t.EndUs / AxisUnitsPerMs, -1));
        }

        points.Sort((p, q) => p.Ms.CompareTo(q.Ms));

        var active = 0;
        var previousMs = points[0].Ms;

        foreach (var (ms, delta) in points)
        {
            if (ms > previousMs && active > 0)
            {
                var x0 = Math.Max(b.StartX, TimeToX(previousMs));
                var x1 = Math.Min(b.EndX, TimeToX(ms));

                if (x1 > x0)
                {
                    // Map 1..DOP concurrent workers onto a 0.7 → 1.3 brightness ramp over the envelope.
                    var intensity = (float)active / workers.Count;

                    _markerPaint.Color = TimelineColours.Scale(b.BarColour, 0.7f + 0.6f * intensity);

                    canvas.DrawRect(x0, b.BarTop, x1 - x0, b.BarBottom - b.BarTop, _markerPaint);
                }
            }

            active += delta;

            previousMs = ms;
        }
    }

    /// <remarks>
    /// Dims the consume (build) phase of a blocking operator on its bar: the span where it is reading its input but has not yet started
    /// emitting rows to its parent (a hash build, a sort's run formation). The undimmed remainder of the bar is the emit phase. Streaming
    /// operators have no consume phase and so are left fully solid.
    /// </remarks>
    private void DrawConsumeShade(SKCanvas canvas, OperatorBar b)
    {
        if (b.Op.BuildPhaseDurationUs <= 0)
        {
            return;
        }

        var consumeStartX = Math.Max(b.StartX, TimeToX(b.Op.BuildPhaseTimeUs / AxisUnitsPerMs));
        var consumeEndX = Math.Min(b.EndX,
            TimeToX((b.Op.BuildPhaseTimeUs + b.Op.BuildPhaseDurationUs) / AxisUnitsPerMs));

        if (consumeEndX <= consumeStartX)
        {
            return;
        }

        // Clip to the bar so the overlay respects its rounded corners.
        canvas.Save();
        canvas.ClipRoundRect(
            new SKRoundRect(new SKRect(b.StartX, b.BarTop, b.EndX, b.BarBottom), b.CornerRadius, b.CornerRadius),
            antialias: true);

        _markerPaint.Color = ConsumeShadeColour;
        canvas.DrawRect(consumeStartX, b.BarTop, consumeEndX - consumeStartX, b.BarBottom - b.BarTop, _markerPaint);

        canvas.Restore();

        _hitRegions.Add((new SKRect(consumeStartX, b.BarTop, consumeEndX, b.BarBottom), b.Op, "Consuming"));
    }

    private void DrawObjectColourMarker(SKCanvas canvas, OperatorBar b)
    {
        if (ColourProvider?.GetObjectColour(b.Op.ObjectName) is not { } colour)
        {
            return;
        }

        // When the bar is too short for the corner dot to sit inside it (the 3px inset plus the dot's 8px height would overflow the
        // bottom), draw a full-height colour band down the bar's left edge instead. Clipping to the bar's rounded rect gives the band the
        // bar's own rounded left corners and keeps its right edge flush inside the bar.
        if (b.BarBottom - b.BarTop < ObjectMarkerMargin + 2 * ObjectMarkerRadius)
        {
            var bandWidth = Math.Min(ObjectMarkerBandWidth, b.EndX - b.StartX);

            if (bandWidth <= 0)
            {
                return;
            }

            canvas.Save();
            canvas.ClipRoundRect(
                new SKRoundRect(new SKRect(b.StartX, b.BarTop, b.EndX, b.BarBottom), b.CornerRadius, b.CornerRadius), antialias: true);

            _markerPaint.Color = colour.ToSkColor();

            canvas.DrawRect(b.StartX, b.BarTop, bandWidth, b.BarBottom - b.BarTop, _markerPaint);

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

        _markerPaint.IsAntialias = true;
        _markerPaint.Color = colour.ToSkColor();

        canvas.DrawCircle(centreX, centreY, ObjectMarkerRadius, _markerPaint);
    }

    /// <summary>
    /// Traces the clicked operator's rows up to the root: a connector for each child→parent hop, lit only over the window the source is
    /// emitting (its non-dimmed span). Because emit time only moves later up the tree, the lit segments form a rising staircase showing
    /// where the flow is held up.
    /// </summary>
    private void DrawRowFlowPath(SKCanvas canvas, List<OperatorBar> bars, int selectedNodeId)
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
            DrawFlowConnector(canvas, chain[i], chain[i + 1]);
        }

        // Outline each operator on the path; the clicked one stands out.
        foreach (var bar in chain)
        {
            var isSelected = bar.Op.PlanNodeIdentifier?.NodeId == selectedNodeId;
            OutlineBar(canvas, bar, isSelected ? FlowSelectedColour : FlowPathColour, isSelected ? 2f : 1f);
        }
    }

    private void DrawFlowConnector(SKCanvas canvas, OperatorBar child, OperatorBar parent)
    {
        // Rows flow from the child while it is emitting: [EmitStart, End].
        var x0 = Math.Max(RowLabelWidth, TimeToX(child.Op.EmitStartUs / AxisUnitsPerMs));

        var x1 = Math.Min(CanvasWidth, TimeToX((child.Op.TimeUs + child.Op.DurationUs) / AxisUnitsPerMs));

        if (x1 <= x0)
        {
            return;
        }

        // Bridge the two bars from the top edge of the upper to the bottom edge of the lower.
        var yLo = Math.Min(child.BarTop, parent.BarTop);

        var yHi = Math.Max(child.BarBottom, parent.BarBottom);

        _flowConnectorPaint.Color = FlowConnectorColour;

        canvas.DrawRect(x0, yLo, x1 - x0, yHi - yLo, _flowConnectorPaint);
    }

    private void OutlineBar(SKCanvas canvas, OperatorBar b, SKColor colour, float strokeWidth)
    {
        _outlinePaint.Color = colour;
        _outlinePaint.StrokeWidth = strokeWidth;

        canvas.DrawRoundRect(new SKRect(b.StartX, b.BarTop, b.EndX, b.BarBottom),
                             b.CornerRadius,
                             b.CornerRadius,
                             _outlinePaint);
    }

    /// <summary>
    /// Draws operator label
    /// </summary>
    /// <remarks>
    /// Dependent on available width and height. Options are:
    ///
    ///     Two lines: Name, Object Name
    ///     One line: Name + Object Name
    ///     One line: Name
    ///     No label
    /// </remarks>
    private void DrawOperatorLabel(SKCanvas canvas,
                                   ExecutionOperatorEvent planOperator,
                                   float startX,
                                   float endX,
                                   float y,
                                   float barHeight)
    {
        var opName = planOperator.Name;

        // Operators with no object of their own (joins, sorts, ...) show their logical operator (e.g. "Inner Join") on the second line
        // instead of leaving it blank.
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

        // Widths scale linearly with font size, so measure once at the cap and derive each layout's
        // width-bound size from that.
        _operatorBoldFont.Size = OperatorMaxFont;
        _operatorFont.Size = OperatorMaxFont;

        var typeWidth = typeSpan.Length > 0 ? _operatorBoldFont.MeasureText(typeSpan) : 0f;
        var targetWidth = target.Length > 0 ? _operatorFont.MeasureText((ReadOnlySpan<char>)target) : 0f;

        var hasBoth = typeSpan.Length > 0 && target.Length > 0;

        _operatorTextPaint.Color = OperatorLabelColour;

        // 1. Two lines: type above the object name, left aligned. Each line scales with the wider of
        // the two, and the pair needs room for two rows of text plus the gap between them.
        if (hasBoth)
        {
            var widerAtMax = Math.Max(typeWidth, targetWidth);

            var sizeByWidth = widerAtMax <= 0 ? OperatorMaxFont : OperatorMaxFont * availableWidth / widerAtMax;

            var sizeByHeight = (barHeight - 2f - TwoLineGap) / 2f;

            var size = Math.Min(OperatorMaxFont, Math.Min(sizeByWidth, sizeByHeight));

            if (size >= OperatorMinFont)
            {
                _operatorBoldFont.Size = size;
                _operatorFont.Size = size;

                var x = startX + textPadX;
                var halfGap = (size + TwoLineGap) / 2f;

                DrawTextSpan(canvas, typeSpan, x, y - halfGap + size * 0.35f, _operatorBoldFont, _operatorTextPaint);
                DrawTextSpan(canvas, target, x, y + halfGap + size * 0.35f, _operatorFont, _operatorTextPaint);
                return;
            }
        }

        // 2. Single line: type then object name, left aligned (the original layout) - when the bar is
        // too short for two lines but wide enough to fit both side by side.
        if (hasBoth)
        {
            var widthAtMax = typeWidth + targetWidth + OperatorMaxFont * OperatorLabelGapFraction;

            var sizeByWidth = widthAtMax <= 0 ? OperatorMaxFont : OperatorMaxFont * availableWidth / widthAtMax;

            var sizeByHeight = barHeight - 2f;

            var size = Math.Min(OperatorMaxFont, Math.Min(sizeByWidth, sizeByHeight));

            if (size >= OperatorMinFont)
            {
                _operatorBoldFont.Size = size;
                _operatorFont.Size = size;

                var baseline = y + size * 0.35f;
                var x = startX + textPadX;

                DrawTextSpan(canvas, typeSpan, x, baseline, _operatorBoldFont, _operatorTextPaint);

                x += _operatorBoldFont.MeasureText(typeSpan, null) + size * OperatorLabelGapFraction;

                DrawTextSpan(canvas, target, x, baseline, _operatorFont, _operatorTextPaint);

                return;
            }
        }

        // 3. Single line: the operator type alone (or whichever single label is present) - when there
        // isn't room for both but the type still fits.
        var isPrimaryType = typeSpan.Length > 0;
        var primarySpan = isPrimaryType ? typeSpan : (ReadOnlySpan<char>)target;
        var primaryFont = isPrimaryType ? _operatorBoldFont : _operatorFont;
        var primaryWAtMax = isPrimaryType ? typeWidth : targetWidth;

        var nameSizeByWidth = primaryWAtMax <= 0 ? OperatorMaxFont : OperatorMaxFont * availableWidth / primaryWAtMax;
        var nameSize = Math.Min(OperatorMaxFont, Math.Min(nameSizeByWidth, barHeight - 2f));

        if (nameSize >= OperatorMinFont)
        {
            primaryFont.Size = nameSize;
            DrawTextSpan(canvas, primarySpan, startX + textPadX, y + nameSize * 0.35f, primaryFont, _operatorTextPaint);
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
}
