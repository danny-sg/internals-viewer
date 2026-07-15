using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Transactions;
using InternalsViewer.Query.Parsing.Plans;
using InternalsViewer.UI.App.Helpers;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline;

public sealed partial class EventTimelineControl
{
    // A discrete lock line is at least this tall; a band that can't fit its concurrency as lines this tall falls back to
    // a density shade instead of sub-pixel lines.
    private const float MinLockLineHeight = 2f;

    // A discrete (non-overlapping) lock line is near-opaque.
    private const byte LockLineAlpha = 220;

    private const float MarkerWidth = 1f;

    // Draws the Lock band as one EQUAL-height sub-band per lock-mode category present (Read/Update/Write/Schema/Range/…),
    // ordered by category, so a busy category can't crowd out the others. Within a band each lock is a bar spanning its
    // held duration; if the band's max concurrency fits as lines at least MinLockLineHeight tall they are packed into
    // sub-lanes (a lone lock fills the band as a bar), otherwise it switches to a density shade — translucent full-height
    // bars that blend darker where more locks are held at once. Shows lock type (band + colour), level (band order),
    // concurrency (lines / shade) and duration (bar width) together.
    private void DrawLockGroups(SKCanvas canvas, float[] rowTops, float[] rowHeights)
    {
        var lockRow = _rows.IndexOf(typeof(LockEvent));

        if (lockRow < 0)
        {
            return;
        }

        var innerTop = rowTops[lockRow] + RowPadding;
        var innerHeight = rowHeights[lockRow] - RowPadding * 2;
        var rightEdge = CanvasWidth;

        // Every lock on the band: the members of each group plus any ungrouped locks.
        var locks = _sortedEvents.OfType<LockGroup>()
                                 .SelectMany(g => g.Events.OfType<LockEvent>())
                                 .Concat(_sortedEvents.OfType<LockEvent>());

        // One band per non-empty category, most exclusive at the top so an escalation to a coarser lock steps UP. Intent
        // modes band apart, below every real lock: they only flag finer locks below the resource rather than holding it,
        // so an IU does not outrank an RS_U despite both being of the update family.
        var categories = locks.GroupBy(l => (Category: LockModeClassifier.Categorise(l.LockMode),
                                             Intent: LockModeClassifier.IsIntent(l.LockMode)))
                              .Where(g => g.Key.Category != LockModeCategory.None)
                              .OrderBy(g => g.Key.Intent)
                              .ThenByDescending(g => TimelineColours.LockCategoryLevel(g.Key.Category))
                              .ToList();

        if (categories.Count == 0)
        {
            return;
        }

        var bandHeight = innerHeight / categories.Count;

        var availableLanes = Math.Max(1, (int)(bandHeight / MinLockLineHeight));

        var cursorY = innerTop;

        foreach (var category in categories)
        {
            var bandTop = cursorY;

            cursorY += bandHeight;

            // Pack into sub-lanes (greedy by start time) so concurrent holds don't overlap; the lane count is the max
            // concurrency in this band.
            var laneEnds = new List<long>();

            var placed = new List<(LockEvent Lock, int Lane)>();

            foreach (var lockEvent in category.OrderBy(l => l.TimeUs))
            {
                var end = lockEvent.TimeUs + Math.Max(0, lockEvent.DurationUs);

                var lane = laneEnds.FindIndex(laneEnd => laneEnd <= lockEvent.TimeUs);

                if (lane < 0)
                {
                    lane = laneEnds.Count;

                    laneEnds.Add(end);
                }
                else
                {
                    laneEnds[lane] = end;
                }

                placed.Add((lockEvent, lane));
            }

            var colour = TimelineColours.LockModeColour(placed[0].Lock.LockMode);

            // Discrete lines while the concurrency fits at a legible height; otherwise a jagged density profile.
            if (laneEnds.Count <= availableLanes)
            {
                var laneHeight = bandHeight / laneEnds.Count;

                foreach (var (lockEvent, lane) in placed)
                {
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

                    var top = bandTop + lane * laneHeight + 0.5f;
                    var height = Math.Max(1f, laneHeight - 1f);

                    var alpha = (byte)(DimForSelection(lockEvent) ? FocusedDimAlpha : LockLineAlpha);

                    _markerPaint.Color = colour.WithAlpha(alpha);
                    canvas.DrawRect(startX, top, endX - startX, height, _markerPaint);

                    _hitRegions.Add((new SKRect(startX - 1, top, endX + 1, top + height), lockEvent, null));
                }
            }
            else
            {
                // When an operator is selected, a band whose locks are all on a different object fades (matches the
                // per-lock dim of the discrete branch).
                var dimmed = placed.All(p => DimForSelection(p.Lock));

                DrawLockDensity(canvas, placed, bandTop, bandHeight, colour, rightEdge, dimmed);
            }
        }

        // Over the bands: escalation is the moment the fine locks below it were dropped for the coarse one above.
        DrawLockEscalations(canvas, innerTop, innerHeight);
    }

    // The measured escalation moment (sqlserver.lock_escalation) as a marker across the whole lock band — it is an
    // instant, not a hold, and it is where every finer lock in that transaction ends.
    private void DrawLockEscalations(SKCanvas canvas, float top, float height)
    {
        for (var i = 0; i < _sortedEvents.Count; i++)
        {
            if (_sortedEvents[i] is not LockEscalationEvent escalation)
            {
                continue;
            }

            var x = TimeToX(_times[i]);

            if (x < RowLabelWidth || x > CanvasWidth)
            {
                continue;
            }

            // The caret head and the stem are unioned into one silhouette, so the dark surround wraps the marker as a
            // whole — stroking them separately leaves a seam where they meet and the head reads as detached.
            using var marker = MarkerPath(x, top, height);

            var wasAntialias = _markerPaint.IsAntialias;
            var wasStyle = _markerPaint.Style;
            var wasStrokeWidth = _markerPaint.StrokeWidth;
            var wasJoin = _markerPaint.StrokeJoin;

            _markerPaint.IsAntialias = true;

            // Stroke the outline first: it straddles the edge, and the fill then covers its inner half, leaving the
            // surround outside the shape. Keeps the marker off a lock band of its own colour.
            _markerPaint.Style = SKPaintStyle.Stroke;
            _markerPaint.StrokeWidth = EscalationOutlineWidth * 2f;
            _markerPaint.StrokeJoin = SKStrokeJoin.Round;
            _markerPaint.Color = EscalationOutlineColour;

            canvas.DrawPath(marker, _markerPaint);

            // Coloured by the mode being escalated TO, so the marker reads as belonging to the band it steps up into.
            _markerPaint.Style = SKPaintStyle.Fill;
            _markerPaint.Color = TimelineColours.LockModeColour(escalation.LockMode);

            canvas.DrawPath(marker, _markerPaint);

            _markerPaint.StrokeJoin = wasJoin;
            _markerPaint.StrokeWidth = wasStrokeWidth;
            _markerPaint.Style = wasStyle;
            _markerPaint.IsAntialias = wasAntialias;

            _hitRegions.Add((new SKRect(x - EscalationCaretSize, top, x + EscalationCaretSize, top + height),
                             escalation,
                             "Lock escalation"));
        }
    }

    // The escalation marker as one shape: a downward caret head on the band's top edge over a stem down the band,
    // unioned so it has a single outline rather than two that meet at a seam.
    private static SKPath MarkerPath(float x, float top, float height)
    {
        using var caret = new SKPath();

        caret.MoveTo(x - EscalationCaretSize, top);
        caret.LineTo(x + EscalationCaretSize, top);
        caret.LineTo(x, top + EscalationCaretSize);
        caret.Close();

        using var stem = new SKPath();

        stem.AddRect(new SKRect(x - 1f, top, x + 1f, top + height));

        // Op returns null if the union fails; the stem alone still marks the instant.
        return caret.Op(stem, SKPathOp.Union) ?? new SKPath(stem);
    }

    // Draws a band's locks as a per-pixel concurrency profile: each column's height is how many locks are held at that
    // moment, so the outline is jagged at each acquire/release the pixel resolution can show and merges (a taller
    // column) only where several boundaries fall in one pixel. Preserves start/end structure a flat shade would hide.
    private void DrawLockDensity(SKCanvas canvas,
                                 List<(LockEvent Lock, int Lane)> locks,
                                 float bandTop,
                                 float bandHeight,
                                 SKColor colour,
                                 float rightEdge,
                                 bool dimmed)
    {
        var x0 = (int)MathF.Floor(RowLabelWidth);
        var x1 = (int)MathF.Ceiling(rightEdge);
        var span = x1 - x0;

        if (span <= 0)
        {
            return;
        }

        // Difference array over pixel columns: +1 at each lock's start, -1 at its end.
        var concurrency = new int[span + 1];

        foreach (var (lockEvent, _) in locks)
        {
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

            var start = (int)MathF.Round(Math.Clamp(startX, x0, x1)) - x0;
            var end = (int)MathF.Round(Math.Clamp(endX, x0, x1)) - x0;

            if (end <= start)
            {
                end = start + 1;
            }

            concurrency[start]++;
            concurrency[Math.Min(end, span)]--;

            _hitRegions.Add((new SKRect(startX - 1, bandTop, endX + 1, bandTop + bandHeight), lockEvent, null));
        }

        // Prefix-sum the deltas into a per-column concurrency, tracking the peak to normalise the height.
        var running = 0;
        var peak = 0;

        for (var i = 0; i < span; i++)
        {
            running += concurrency[i];
            concurrency[i] = running;

            if (running > peak)
            {
                peak = running;
            }
        }

        if (peak <= 0)
        {
            return;
        }

        var bandBottom = bandTop + bandHeight;

        _markerPaint.Color = colour.WithAlpha(dimmed ? FocusedDimAlpha : (byte)230);

        for (var i = 0; i < span; i++)
        {
            var count = concurrency[i];

            if (count <= 0)
            {
                continue;
            }

            // At least MinLockLineHeight so an isolated lock still shows, scaling to the full band at the peak.
            var height = MinLockLineHeight + (bandHeight - MinLockLineHeight) * count / peak;

            canvas.DrawRect(x0 + i, bandBottom - height, 1f, height, _markerPaint);
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

    // An event fades when an operator is selected and the event doesn't belong to it. Plan-matched events (reads, log)
    // compare on node id; locks carry no plan node, so they highlight with the selection when they are on the same
    // object (table) as the selected operator, and fade otherwise.
    private bool DimForSelection(EngineEvent ev)
    {
        if (_selectedNodeId is not { } selected)
        {
            return false;
        }

        if (ev.PlanNodeIdentifier is { } id)
        {
            return id.NodeId != selected;
        }

        if (ev is LockEvent && !string.IsNullOrEmpty(_selectedTable))
        {
            return !string.Equals(ev.TableName, _selectedTable, StringComparison.OrdinalIgnoreCase)
                   || !string.Equals(ev.SchemaName, _selectedSchema, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
