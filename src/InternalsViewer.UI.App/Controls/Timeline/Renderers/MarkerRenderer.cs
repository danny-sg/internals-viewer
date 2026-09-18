using System;
using System.Collections.Generic;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline.Renderers;

/// <summary>
/// Draws the point-event ticks — reads, waits, latches and log — as a tick per event in its lane, with a faint full-duration overlay behind
/// events that span time
/// </summary>
/// <remarks>
/// Operators and locks are drawn by their own renderers, so this covers every other lane: the Read band splits into
/// cached/non-cached halves, the mixed Wait/Latch lanes step their tick by event category, and the rest fill their lane.
/// </remarks>
internal sealed class MarkerRenderer(RenderResource resources, CurrentSelection selection, List<HitRegion> hitRegions)
{
    // A spanning event's duration overlay is capped this translucent so it reads as a background hint behind the tick.
    private const byte DurationOverlayAlpha = 96;

    // Mirrors the control's focused-dim tier: an event on a different operator than the selected one fades to this.
    private const byte DimAlpha = 70;

    // Half-width of the hit region's horizontal padding, so a thin tick is still easy to hover.
    private const float HitPad = 3f;

    // The widest a tick can be (sparse rows): used as the left cull margin so a marker just off the label edge still culls.
    private const float MaxMarkerWidth = 4f;

    private static readonly SKColor SegmentEliminationColour = ColourConstants.SegmentEliminationColour.ToSkColor();

    private static readonly SKColor ObjectPoolHitColour = ColourConstants.ObjectPoolHitColour.ToSkColor();

    private static readonly SKColor ObjectPoolMissColour = ColourConstants.ObjectPoolMissColour.ToSkColor();

    private static readonly SKColor ColumnStoreEventColour = ColourConstants.ColumnStoreEventColour.ToSkColor();

    private static readonly SKColor ExpressionFilterBitmapColour = ColourConstants.ExpressionFilterBitmapColour.ToSkColor();

    public void Draw(SKCanvas canvas, TimelineFrame frame)
    {
        var events = frame.Events;

        for (var i = 0; i < events.Count; i++)
        {
            var sourceEvent = events[i];

            // Operators and locks have their own renderers (OperatorRenderer / LockBandRenderer).
            if (sourceEvent is ExecutionOperatorEvent or LockEvent)
            {
                continue;
            }

            var rowIndex = frame.Rows.IndexOf(sourceEvent);

            if (rowIndex < 0)
            {
                continue;
            }

            var innerTop = frame.RowTops[rowIndex] + frame.RowPadding;
            var innerHeight = frame.RowHeights[rowIndex] - frame.RowPadding * 2;

            float markerTop;
            float markerHeight;

            // The Read lane holds only ReadEventGroup; nulling the category routes it through the per-node colour
            // provider (Kind-based) instead of the flat category tint used by the mixed Wait/Latch lanes.
            var category = sourceEvent is ReadEventGroup or IoEvent ? null : sourceEvent.Category;

            if (sourceEvent is ReadEventGroup or IoEvent)
            {
                // The read band is split into two lanes: cached (buffer-pool) reads on the top half, non-cached (physical) reads on the
                // bottom half.
                var laneHeight = innerHeight / 2f;

                var isCached = sourceEvent is ReadEventGroup { ReadType: ReadType.Cached };

                markerTop = innerTop + (isCached ? 0f : laneHeight);
                markerHeight = Math.Max(2f, laneHeight - 1f);
            }
            else if (sourceEvent is SegmentScanEvent or SegmentEliminateEvent or ObjectPoolEvent or ColumnStoreScanEvent)
            {
                var laneHeight = innerHeight / 2f;

                if (sourceEvent is SegmentScanEvent)
                {
                    var subLaneHeight = laneHeight / frame.SegmentLanes.LaneCount;

                    markerTop = innerTop + frame.SegmentLanes.LaneOf(i) * subLaneHeight;
                    markerHeight = Math.Max(2f, subLaneHeight - 2f);
                }
                else if (sourceEvent is SegmentEliminateEvent or ColumnStoreScanEvent { IsRowGroupEvent: true })
                {
                    markerTop = innerTop;
                    markerHeight = Math.Max(2f, laneHeight - 1f);
                }
                else if (sourceEvent is ObjectPoolEvent && frame.TryGetPoolMarker(i, out var poolTop, out var poolHeight))
                {
                    markerTop = poolTop;
                    markerHeight = poolHeight;
                }
                else
                {
                    markerTop = innerTop + laneHeight;
                    markerHeight = Math.Max(2f, laneHeight - 1f);
                }
            }
            else if (category.HasValue)
            {
                var stepHeight = innerHeight / EventCategoryClassifier.CategoryCount;
                var step = (int)category.Value;

                markerTop = innerTop + step * stepHeight;
                markerHeight = Math.Max(2f, stepHeight - 1f);
            }
            else
            {
                markerTop = innerTop;
                markerHeight = innerHeight;
            }

            var markerColour = GetMarkerColour(frame, sourceEvent, rowIndex, category);

            var markerWidth = frame.RowMarkerWidth(rowIndex);

            var startX = frame.TimeToX(frame.Times[i]);

            var hasDuration = sourceEvent.DurationUs > 0;

            var endX = hasDuration
                ? frame.TimeToX(frame.Times[i] + sourceEvent.DurationUs / frame.AxisUnitsPerMs)
                : startX + markerWidth;

            if (hasDuration && endX < startX + markerWidth)
            {
                endX = startX + markerWidth;
            }

            if (endX < frame.RowLabelWidth - MaxMarkerWidth || startX > frame.CanvasWidth)
            {
                continue;
            }

            if (hasDuration)
            {
                var isSolid = sourceEvent is ObjectPoolEvent or SegmentScanEvent;

                resources.Fill.Color = isSolid
                    ? markerColour
                    : markerColour.WithAlpha(Math.Min(markerColour.Alpha, DurationOverlayAlpha));

                canvas.DrawRect(startX, markerTop, endX - startX, markerHeight, resources.Fill);
            }

            resources.Fill.Color = markerColour;

            var tickX = sourceEvent is ReadEventGroup && hasDuration ? endX - markerWidth : startX;

            canvas.DrawRect(tickX, markerTop, markerWidth, markerHeight, resources.Fill);

            hitRegions.Add(new HitRegion(new SKRect(startX - HitPad, markerTop, endX + HitPad, markerTop + markerHeight),
                                          sourceEvent,
                                          null));
        }
    }

    private SKColor GetMarkerColour(TimelineFrame frame, EngineEvent sourceEvent, int rowIndex, EventCategory? category)
    {
        var laneColour = frame.Rows.Active[rowIndex].Color;

        var colour = sourceEvent switch
        {
            SegmentEliminateEvent 
                => SegmentEliminationColour,
            ObjectPoolEvent { IsHit: true } 
                => ObjectPoolHitColour,
            ObjectPoolEvent 
                => ObjectPoolMissColour,
            ColumnStoreScanEvent { EventName: "column_store_expression_filter_bitmap_set" }
                => ExpressionFilterBitmapColour,
            ColumnStoreScanEvent 
                => ColumnStoreEventColour,
            SegmentScanEvent 
                => laneColour,
            _ when category.HasValue 
                => TimelineColours.TintByCategory(laneColour, (int)category.Value),
            _ when frame.ColourProvider is { } colours 
                => colours.GetColour(sourceEvent).ToSkColor(),
            _ => laneColour,
        };

        return colour.WithAlpha(selection.ShouldDim(sourceEvent) ? DimAlpha : (byte)255);
    }
}
