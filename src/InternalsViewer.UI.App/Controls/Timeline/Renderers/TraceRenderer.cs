using System.Collections.Generic;
using System;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Transactions;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Helpers;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline.Renderers;

/// <summary>
/// Draws the I/O and log trace extensions: faint rails that connect an operator bar to the reads it issued (below) and
/// the log records it wrote (above)
/// </summary>
/// <remarks>
/// Reads are modelled as a Volcano call/return — a dotted call rail to the top of the read, then one solid return rail
/// per page moved, bunched at the read's end. Everything is composited through one translucent layer so overlapping
/// rails merge once rather than stacking to full opacity.
/// </remarks>
internal sealed class TraceRenderer(RenderResource resources, CurrentSelection selection)
{
    // The dotted call rail is only drawn when the read is at least this wide, so it reads as distinct from the end rail.
    private const float MinCallRailGapPx = 4f;

    // Per-page return rails are spread back from the read's end by this gap, so a denser cluster reads as more pages.
    private const float PageRailGapPx = 3f;

    // Mirrors the control's focused-dim tier: a trace belonging to a non-selected operator fades to this.
    private const byte DimAlpha = 70;

    public void Draw(SKCanvas canvas, TimelineFrame frame, IReadOnlyList<OperatorBar> bars)
    {
        var byNode = new Dictionary<PlanNodeIdentifier, OperatorBar>(bars.Count);

        foreach (var b in bars)
        {
            if (b.Op.PlanNodeIdentifier is { } id)
            {
                byNode[id] = b;
            }
        }

        var ioRow = frame.Rows.IndexOf(typeof(ReadEventGroup));
        var logRow = frame.Rows.IndexOf(typeof(TransactionLogEvent));

        if (ioRow < 0 && logRow < 0)
        {
            return;
        }

        var rightEdge = frame.CanvasWidth;

        // Composite all extensions through a single layer at reduced opacity so overlapping traces don't stack up to
        // full opacity (the layer merges them first, then fades the whole thing once). Bound the layer to just the rows
        // traces reach — the operator bars plus the read/log lanes — so Skia allocates a band-sized offscreen rather
        // than a full-canvas one on every playback (the cost otherwise scales with the whole control's size).
        canvas.SaveLayer(TraceBounds(frame, bars, ioRow, logRow, rightEdge), resources.TraceLayer);

        if (ioRow >= 0)
        {
            var readTop = frame.RowTops[ioRow] + frame.RowPadding;

            var readBottom = frame.RowTops[ioRow] + frame.RowHeights[ioRow] - frame.RowPadding;

            var width = frame.RowMarkerWidth(ioRow);

            for (var i = 0; i < frame.Events.Count; i++)
            {
                if (frame.Events[i] is not ReadEventGroup { PlanNodeIdentifier: { } id } io ||
                    !byNode.TryGetValue(id, out var b) ||
                    b.BarBottom >= readTop)
                {
                    continue;
                }

                var startX = frame.TimeToX(frame.Times[i]);

                var endX = io.DurationUs > 0 ? frame.TimeToX(frame.Times[i] + io.DurationUs / frame.AxisUnitsPerMs) : startX;

                if (startX > rightEdge || endX < frame.RowLabelWidth - width)
                {
                    continue;
                }

                var colour = TraceColour(frame, io, ioRow);

                // Terminate the rails at the read's own lane (cached = top half, non-cached = bottom half) so they line
                // up with the split read band.
                var laneHeight = (readBottom - readTop) / 2f;
                var railTop = io.ReadType == ReadType.Cached ? readTop : readTop + laneHeight;
                var railBottom = io.ReadType == ReadType.Cached ? readTop + laneHeight : readBottom;

                // Only draw the dotted call rail when it is far enough left of the end return rail to read as distinct.
                if (endX - startX > MinCallRailGapPx)
                {
                    resources.ReadCallRail.Color = colour;
                    canvas.DrawLine(startX, b.BarBottom, startX, railTop, resources.ReadCallRail);
                }

                resources.ReadReturnRail.Color = colour;

                // The pages land in the buffer together when the I/O completes, so — absent real per-page timing — the
                // return rails bunch at the read's END, only slightly separated for legibility. A single-page read is
                // therefore one rail at the end.
                var pageCount = Math.Max(1, io.PageCount);

                for (var p = 0; p < pageCount; p++)
                {
                    var x = Math.Max(startX, endX - p * PageRailGapPx);

                    canvas.DrawLine(x, b.BarBottom, x, railBottom, resources.ReadReturnRail);
                }
            }
        }

        if (logRow >= 0)
        {
            // Log writes are above the plan: extend from the log row down to the modification operator's top.
            var logBottom = frame.RowTops[logRow] + frame.RowHeights[logRow] - frame.RowPadding;
            var width = frame.RowMarkerWidth(logRow);

            for (var i = 0; i < frame.Events.Count; i++)
            {
                if (frame.Events[i] is not TransactionLogEvent { PlanNodeIdentifier: { } id } log ||
                    !byNode.TryGetValue(id, out var b) ||
                    b.BarTop <= logBottom)
                {
                    continue;
                }

                var x = frame.TimeToX(frame.Times[i]);

                if (x > rightEdge || x < frame.RowLabelWidth - width)
                {
                    continue;
                }

                resources.Fill.Color = TraceColour(frame, log, logRow);
                canvas.DrawRect(x, logBottom, width, b.BarTop - logBottom, resources.Fill);
            }
        }

        canvas.Restore();
    }

    // The vertical span the rails occupy: the operator bars they drop from, plus the read and log lanes they reach.
    // Used to size the composite layer's offscreen to the band instead of the whole canvas.
    private static SKRect TraceBounds(TimelineFrame frame, IReadOnlyList<OperatorBar> bars, int ioRow, int logRow, float rightEdge)
    {
        var top = float.MaxValue;
        var bottom = float.MinValue;

        foreach (var b in bars)
        {
            top = Math.Min(top, b.BarTop);

            bottom = Math.Max(bottom, b.BarBottom);
        }

        if (ioRow >= 0)
        {
            bottom = Math.Max(bottom, frame.RowTops[ioRow] + frame.RowHeights[ioRow]);
        }

        if (logRow >= 0)
        {
            top = Math.Min(top, frame.RowTops[logRow]);
        }

        return new SKRect(frame.RowLabelWidth, top, rightEdge, bottom);
    }

    // A trace takes its operator's per-node colour (or the flat lane colour when no provider is set), faded when the
    // event belongs to an operator other than the selected one.
    private SKColor TraceColour(TimelineFrame frame, EngineEvent ev, int rowIndex)
    {
        var colour = frame.ColourProvider is { } colours
            ? colours.GetColour(ev).ToSkColor()
            : frame.Rows.Active[rowIndex].Color;

        return colour.WithAlpha(selection.ShouldDim(ev) ? DimAlpha : (byte)255);
    }
}
