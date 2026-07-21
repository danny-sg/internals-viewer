using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.Events.Locks;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline.Renderers;

/// <summary>
/// Draws the Lock lane
/// </summary>
/// <remarks>
/// One category sub-band per lock-mode present each a concurrency bar chart, with escalation markers over the top
/// 
/// The bands are ordered by category (most exclusive on top) so an escalation to a coarser lock steps up, with intent modes banded apart
/// below the real locks. Within a band the locks are a single concurrency bar chart.
///
/// Column height grows with the number of locks held at that instant, so overlaps rise above the baseline as a taller bar rather than
/// splitting into separate lanes, and the band reads the same for two overlaps as for thousands.
/// </remarks>
internal sealed class LockRenderer(RenderResource resources, CurrentSelection selection, List<HitRegion> hitRegions)
    : IDisposable
{
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

        var locks = frame.Events.OfType<LockGroup>()
                                .SelectMany(g => g.Events.OfType<LockEvent>())
                                .Concat(frame.Events.OfType<LockEvent>());

        // Locks grouped into category and intent, ordered by category and intent, replicating lock hierarchy
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

            var isDimmed = bandLocks.All(selection.ShouldDim);

            DrawCategory(canvas, frame, bandLocks, bandTop, bandHeight, colour, rightEdge, isDimmed, category.Key.Intent);
        }

        DrawEscalationPoints(canvas, frame, innerTop, innerHeight);
    }

    private void DrawCategory(SKCanvas canvas,
                              TimelineFrame frame,
                              IReadOnlyList<LockEvent> locks,
                              float bandTop,
                              float bandHeight,
                              SKColor colour,
                              float rightEdge,
                              bool isDimmed,
                              bool intent)
    {
        var x0 = (int)MathF.Floor(frame.RowLabelWidth);
        var x1 = (int)MathF.Ceiling(rightEdge);

        var span = x1 - x0;

        if (span <= 0)
        {
            return;
        }

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

        resources.Fill.Color = colour.WithAlpha(isDimmed ? DimAlpha : intent ? IntentLockBarAlpha : LockBarAlpha);

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

    /// <summary>
    /// Draw lock escalation points as markers on the band
    /// </summary>
    private void DrawEscalationPoints(SKCanvas canvas, TimelineFrame frame, float top, float height)
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

        return caret.Op(stem, SKPathOp.Union) ?? new SKPath(stem);
    }

    public void Dispose() => _pathBuilder.Dispose();
}
