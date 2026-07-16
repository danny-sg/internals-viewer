using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.Events.Locks;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline.Renderers;

/// <summary>
/// Draws the Lock lane: one category sub-band per lock-mode present, each a concurrency bar chart, with escalation
/// markers over the top
/// </summary>
/// <remarks>
/// The bands are ordered by category (most exclusive on top) so an escalation to a coarser lock steps UP, with intent
/// modes banded apart below the real locks. Within a band the locks are a single concurrency bar chart: column height
/// grows with the number of locks held at that instant, so overlaps rise above the baseline as a taller bar rather than
/// splitting into separate lanes, and the band reads the same for two overlaps as for thousands.
/// </remarks>
internal sealed class LockRenderer(RenderResource resources, CurrentSelection selection, List<HitRegion> hitRegions)
    : IDisposable
{
    // Baseline height for a stretch where a single lock is held, so a lone lock still reads clearly before any overlap
    // adds height above it.
    private const float MinLockBarHeight = 2f;

    private const byte LockBarAlpha = 230;
    private const byte IntentLockBarAlpha = 128;

    private const byte DimAlpha = 70;

    private const float MarkerWidth = 1f;

    private const float EscalationCaretSize = 4f;
    private const float EscalationOutlineWidth = 1.5f;

    private static readonly SKColor EscalationOutlineColour = new(10, 10, 10, 235);

    private const float BandGap = 1f;

    private readonly SKPathBuilder _pathBuilder = new();

    public void Draw(SKCanvas canvas, TimelineFrame frame)
    {
        var lockRow = frame.Rows.IndexOf(typeof(LockEvent));

        if (lockRow < 0)
        {
            return;
        }

        var innerTop = frame.RowTops[lockRow] + frame.RowPadding;
        var innerHeight = frame.RowHeights[lockRow] - frame.RowPadding * 2;
        var rightEdge = frame.CanvasWidth;

        // Every lock on the band: the members of each group plus any ungrouped locks.
        var locks = frame.Events.OfType<LockGroup>()
                                .SelectMany(g => g.Events.OfType<LockEvent>())
                                .Concat(frame.Events.OfType<LockEvent>());

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

        var bandHeight = (innerHeight - BandGap * (categories.Count - 1)) / categories.Count;

        var cursorY = innerTop;

        foreach (var category in categories)
        {
            var bandTop = cursorY;

            cursorY += bandHeight + BandGap;

            var bandLocks = category.OrderBy(l => l.TimeUs).ToList();

            var colour = TimelineColours.LockModeColour(bandLocks[0].LockMode);

            // When an operator is selected, a band fades only when every one of its locks is on a different object;
            // any on-object lock keeps the band lit.
            var dimmed = bandLocks.All(selection.ShouldDim);

            DrawConcurrency(canvas, frame, bandLocks, bandTop, bandHeight, colour, rightEdge, dimmed, category.Key.Intent);
        }

        DrawEscalations(canvas, frame, innerTop, innerHeight);
    }

    // Draws a band's locks as a per-pixel concurrency bar chart: each column's height is how many locks are held at that
    // moment, normalised so the band's peak concurrency fills it. A band with no overlap is a row of full-height bars; a
    // couple of overlaps step up from a compressed baseline to the full band; a dense band tapers with its concurrency
    // — one scaling law across the whole range, so overlaps read as added height rather than as a separate lane.
    private void DrawConcurrency(SKCanvas canvas,
                                 TimelineFrame frame,
                                 IReadOnlyList<LockEvent> locks,
                                 float bandTop,
                                 float bandHeight,
                                 SKColor colour,
                                 float rightEdge,
                                 bool dimmed,
                                 bool intent)
    {
        var x0 = (int)MathF.Floor(frame.RowLabelWidth);
        var x1 = (int)MathF.Ceiling(rightEdge);

        var span = x1 - x0;

        if (span <= 0)
        {
            return;
        }

        // Difference array over pixel columns: +1 at each lock's start, -1 at its end.
        var concurrency = new int[span + 1];

        foreach (var lockEvent in locks)
        {
            var startX = frame.TimeToX(lockEvent.TimeUs / frame.AxisUnitsPerMs);

            var endX = lockEvent.DurationUs > 0
                       ? frame.TimeToX((lockEvent.TimeUs + lockEvent.DurationUs) / frame.AxisUnitsPerMs)
                       : startX + MarkerWidth;

            if (endX < startX + MarkerWidth)
            {
                endX = startX + MarkerWidth;
            }

            if (endX < frame.RowLabelWidth || startX > rightEdge)
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

            hitRegions.Add(new HitRegion(new SKRect(startX - 1, bandTop, endX + 1, bandTop + bandHeight), lockEvent, null));
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

        resources.Fill.Color = colour.WithAlpha(dimmed ? DimAlpha : intent ? IntentLockBarAlpha : LockBarAlpha);

        for (var i = 0; i < span; i++)
        {
            var count = concurrency[i];

            if (count <= 0)
            {
                continue;
            }

            var height = MinLockBarHeight + (bandHeight - MinLockBarHeight) * MathF.Sqrt((float)count / peak);

            canvas.DrawRect(x0 + i, bandBottom - height, 1f, height, resources.Fill);
        }
    }

    private void DrawEscalations(SKCanvas canvas, TimelineFrame frame, float top, float height)
    {
        var events = frame.Events;

        for (var i = 0; i < events.Count; i++)
        {
            if (events[i] is not LockEscalationEvent escalation)
            {
                continue;
            }

            var x = frame.TimeToX(frame.Times[i]);

            if (x < frame.RowLabelWidth || x > frame.CanvasWidth)
            {
                continue;
            }

            using var marker = DrawEscalationHead(x, top, height);

            resources.Stroke.StrokeWidth = EscalationOutlineWidth * 2f;
            resources.Stroke.Color = EscalationOutlineColour;

            canvas.DrawPath(marker, resources.Stroke);

            resources.AntialiasedFill.Color = TimelineColours.LockModeColour(escalation.LockMode);

            canvas.DrawPath(marker, resources.AntialiasedFill);

            hitRegions.Add(new HitRegion(new SKRect(x - EscalationCaretSize, top, x + EscalationCaretSize, top + height),
                                         escalation,
                                         "Lock escalation"));
        }
    }

    private SKPath DrawEscalationHead(float x, float top, float height)
    {
        _pathBuilder.MoveTo(x - EscalationCaretSize, top);
        _pathBuilder.LineTo(x + EscalationCaretSize, top);
        _pathBuilder.LineTo(x, top + EscalationCaretSize);
        _pathBuilder.Close();

        using var caret = _pathBuilder.Detach();

        _pathBuilder.AddRect(new SKRect(x - 1f, top, x + 1f, top + height));

        using var stem = _pathBuilder.Detach();

        // Op returns null if the union fails; the stem alone still marks the instant.
        return caret.Op(stem, SKPathOp.Union) ?? new SKPath(stem);
    }

    public void Dispose() => _pathBuilder.Dispose();
}
