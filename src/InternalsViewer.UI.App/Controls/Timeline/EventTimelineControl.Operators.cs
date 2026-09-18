using System.Collections.Generic;
using System.Linq;
using System;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Operators;
using InternalsViewer.UI.App.Helpers;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline;

public sealed partial class EventTimelineControl
{
    private const float OperatorLineMargin = 3f;

    // Extra per-block padding added in Trace mode so stacked bars leave a gap for the trace lines.
    private const float TraceStackGap = 12f;

    // Buffer-category operators (spool/sort/exchange) are drawn as a thin collapsed bar.
    private const float BufferHeightScale = 0.3f;

    // Data-access (scan/seek) bars are sized within their slot by rows processed; this is the smallest
    // fill fraction so even a tiny scan stays visible.
    private const float DataAccessMinFill = 0.15f;

    // Cost-weighted slot sizing: the statement band's fixed share, and the min/max weight a costed operator maps to.
    private const float StatementLaneWeight = 0.5f;
    private const float MinCostWeight = 0.35f;
    private const float MaxCostWeight = 1.5f;

    // The statement (SELECT) node draws as a single grey bar; BuildOperatorBars uses this for level-0 operators.
    private static readonly SKColor StatementColour = new(130, 130, 130);

    // Lays out the operator bars for this frame: a cost-weighted vertical slot per operator, with the bar sized within
    // its slot by kind (thin for buffer operators, row-count-scaled for data access, full for the rest). Shared by the
    // trace rails (which drop from a bar) and the operator bar drawing.
    private List<OperatorBar> BuildOperatorBars(float[] bandTops, float[] bandHeights)
    {
        var bands = _bands.Active;

        var planBand = -1;

        for (var r = 0; r < bands.Count; r++)
        {
            if (bands[r].Key == typeof(ExecutionOperatorEvent))
            {
                planBand = r; break;
            }
        }

        if (planBand < 0)
        {
            return [];
        }

        var ordered = _orderedOperators;

        if (ordered.Count == 0)
        {
            return [];
        }

        var maxCost = _maxCost;
        var maxRows = _maxRows;

        var top = bandTops[planBand] + BandPadding;
        var height = bandHeights[planBand] - BandPadding * 2;

        var weights = new float[ordered.Count];

        for (var i = 0; i < ordered.Count; i++)
        {
            weights[i] = CostWeight(ordered[i].Op);
        }

        var totalWeight = weights.Sum();

        var laneHeights = OperatorLaneLayout.Resolve(weights, totalWeight, height);

        var laneByIndex = new Dictionary<int, (float Y, float Height)>(ordered.Count);

        var laneAcc = top;

        for (var i = 0; i < ordered.Count; i++)
        {
            var lane = laneHeights[i];

            laneByIndex[ordered[i].Index] = (laneAcc + lane / 2f, lane);
            laneAcc += lane;
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
            var (y, laneHeight) = laneByIndex[index];

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
                    : bands[planBand].Colour;
            }

            // Lay the bar out within the slot. Buffer operators collapse to a thin bar; everything else
            // fills the slot less a margin.
            var laneTop = y - laneHeight / 2f;
            var laneBottom = y + laneHeight / 2f;

            // In Trace mode add extra padding so stacked bars leave a gap for the trace lines to show.
            var effectiveMargin = OperatorLineMargin + TraceStackGap;

            var pad = effectiveMargin / 2f;

            var availTop = laneTop + pad;
            var availBottom = Math.Max(availTop + 1f, laneBottom - pad);

            float barTop, barBottom;

            if (op.Category == OperatorCategory.Buffer)
            {
                // Collapse buffer operators (spool/sort/exchange) to a thin bar centred in the band.
                var barHeight = Math.Max(1f, (laneHeight - effectiveMargin) * BufferHeightScale);
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
                                     lineWidth, cornerRadius, y, laneHeight, barColour));
        }

        return bars;

        float CostWeight(ExecutionOperatorEvent op)
        {
            if (op.NodeLevel == 0)
            {
                return StatementLaneWeight;
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
}
