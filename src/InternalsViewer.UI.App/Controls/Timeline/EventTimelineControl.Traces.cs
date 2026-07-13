using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Plans;
using InternalsViewer.UI.App.Helpers;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline;

public sealed partial class EventTimelineControl
{
    // Draws each lock group's locks with their granularity levels in their OWN lanes — object at the top, page in the
    // middle, row at the bottom — so a held object lock (a wide bar up top) can't eclipse the finer locks: they show as
    // their own teeth in the lanes below it. Individual grouped locks aren't drawn elsewhere (the grid holds the
    // per-lock detail); escalation reads as the finer lanes filling in / the coarse lane taking over over time.
    private void DrawLockGroups(SKCanvas canvas, float[] rowTops, float[] rowHeights)
    {
        var lockRow = _rows.IndexOf(typeof(LockEvent));

        if (lockRow < 0)
        {
            return;
        }

        var innerTop = rowTops[lockRow] + RowPadding;
        var innerHeight = rowHeights[lockRow] - RowPadding * 2;
        var laneHeight = innerHeight / LockLevels;
        var rightEdge = CanvasWidth;

        foreach (var group in _sortedEvents.OfType<LockGroup>())
        {
            // Two dimensions: the lane (Y) is resource escalation (row -> page -> object), the colour is the lock mode.
            var alpha = (byte)(DimForSelection(group) ? FocusedDimAlpha : LockOverlayAlpha);

            foreach (var lockEvent in group.Events.OfType<LockEvent>())
            {
                var level = TimelineColours.GranularityLevel(lockEvent.Resource.ResourceType);

                // Level 0 (row) sits in the bottom lane, the coarsest (object) at the top.
                var markerTop = innerTop + (LockLevels - 1 - level) * laneHeight + 0.5f;
                var markerHeight = Math.Max(1f, laneHeight - 1f);

                var fill = TimelineColours.LockModeColour(lockEvent.LockMode).WithAlpha(alpha);

                var startX = TimeToX(lockEvent.TimeUs / AxisUnitsPerMs);

                var endX = lockEvent.DurationUs > 0
                    ? TimeToX((lockEvent.TimeUs + lockEvent.DurationUs) / AxisUnitsPerMs)
                    : startX + MarkerWidth;

                if (endX < startX + MarkerWidth)
                {
                    endX = startX + MarkerWidth;
                }

                if (endX < RowLabelWidth || startX > rightEdge)
                {
                    continue;
                }

                _markerPaint.Color = fill;
                canvas.DrawRect(startX, markerTop, endX - startX, markerHeight, _markerPaint);

                _hitRegions.Add((new SKRect(startX - 1, markerTop, endX + 1, markerTop + markerHeight), lockEvent, null));
            }
        }
    }

    private void DrawTraces(SKCanvas canvas, List<OperatorBar> bars, float[] rowTops, float[] rowHeights)
    {
        var byNode = new Dictionary<PlanNodeIdentifier, OperatorBar>(bars.Count);

        foreach (var b in bars)
        {
            if (b.Op.PlanNodeIdentifier is { } id)
            {
                byNode[id] = b;
            }
        }

        var ioRow = _rows.IndexOf(typeof(ReadEventGroup));
        var logRow = _rows.IndexOf(typeof(TransactionLogEvent));

        if (ioRow < 0 && logRow < 0)
        {
            return;
        }

        // Composite all extensions through a single layer at reduced opacity so overlapping traces
        // don't stack up to full opacity (the layer merges them first, then fades the whole thing once).
        canvas.SaveLayer(_traceLayerPaint);

        var rightEdge = CanvasWidth;

        if (ioRow >= 0)
        {
            // Reads are below the plan and modelled as a Volcano call/return: a dotted call rail drops from the
            // operator to the TOP of the read (the iterator asking for rows) at the read's start, and one solid return
            // rail per page it moved drops from the operator through the FULL height of the read. The per-page rails are
            // spread evenly across the read's span, so a denser cluster of rails reads as more pages moved.
            var readTop = rowTops[ioRow] + RowPadding;

            var readBottom = rowTops[ioRow] + rowHeights[ioRow] - RowPadding;

            var width = RowMarkerWidth(ioRow);

            for (var i = 0; i < _sortedEvents.Count; i++)
            {
                if (_sortedEvents[i] is not ReadEventGroup { PlanNodeIdentifier: { } id } io ||
                    !byNode.TryGetValue(id, out var b) ||
                    b.BarBottom >= readTop)
                {
                    continue;
                }

                var startX = TimeToX(_times[i]);

                var endX = io.DurationUs > 0 ? TimeToX(_times[i] + DurationMs(io)) : startX;

                if (startX > rightEdge || endX < RowLabelWidth - width)
                {
                    continue;
                }

                var colour = TraceColour(io, ioRow, DimForSelection(io));

                // Terminate the rails at the read's own lane (cached = top half, non-cached = bottom half) so they line
                // up with the split read band.
                var laneHeight = (readBottom - readTop) / 2f;
                var railTop = io.ReadType == ReadType.Cached ? readTop : readTop + laneHeight;
                var railBottom = io.ReadType == ReadType.Cached ? readTop + laneHeight : readBottom;

                // Only draw the dotted call rail when it is far enough left of the end return rail to read as distinct.
                if (endX - startX > MinCallRailGapPx)
                {
                    _readBoundaryPaint.Color = colour;
                    canvas.DrawLine(startX, b.BarBottom, startX, railTop, _readBoundaryPaint);
                }

                _readReturnPaint.Color = colour;

                // The pages land in the buffer together when the I/O completes, so — absent real per-page timing — the
                // return rails bunch at the read's END, only slightly separated for legibility. A single-page read is
                // therefore one rail at the end.
                var pageCount = Math.Max(1, io.PageCount);

                for (var p = 0; p < pageCount; p++)
                {
                    var x = Math.Max(startX, endX - p * PageRailGapPx);

                    canvas.DrawLine(x, b.BarBottom, x, railBottom, _readReturnPaint);
                }
            }
        }

        if (logRow >= 0)
        {
            // Log writes are above the plan: extend from the log row down to the modification operator's top.
            var logBottom = rowTops[logRow] + rowHeights[logRow] - RowPadding;
            var width = RowMarkerWidth(logRow);

            for (var i = 0; i < _sortedEvents.Count; i++)
            {
                if (_sortedEvents[i] is not TransactionLogEvent { PlanNodeIdentifier: { } id } log ||
                    !byNode.TryGetValue(id, out var b) ||
                    b.BarTop <= logBottom)
                {
                    continue;
                }

                var x = TimeToX(_times[i]);
                if (x > rightEdge || x < RowLabelWidth - width)
                {
                    continue;
                }

                _markerPaint.Color = TraceColour(log, logRow, DimForSelection(log));
                canvas.DrawRect(x, logBottom, width, b.BarTop - logBottom, _markerPaint);
            }
        }

        canvas.Restore();
    }

    private SKColor TraceColour(EngineEvent ev, int rowIndex, bool dimmed) =>
        (ColourProvider is { } colours ? colours.GetColour(ev).ToSkColor() : _rows.Active[rowIndex].Color)
            .WithAlpha(dimmed ? FocusedDimAlpha : (byte)255);

    // True when an operator is selected and the event belongs to a different operator, so its marker and
    // trace line are faded to let the selected block's I/O and trace lines stand out.
    private bool DimForSelection(EngineEvent ev) =>
        _selectedNodeId is { } selected && ev.PlanNodeIdentifier is { } id && id.NodeId != selected;
}
