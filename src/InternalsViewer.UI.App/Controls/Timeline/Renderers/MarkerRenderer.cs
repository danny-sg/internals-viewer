using System;
using System.Collections.Generic;
using InternalsViewer.Query.Events;
using InternalsViewer.UI.App.Controls.Timeline.Definition;
using InternalsViewer.UI.App.Helpers;
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

    public void Draw(SKCanvas canvas, TimelineFrame frame)
    {
        var items = frame.Definition.Items;

        var topLayer = 0;

        foreach (var item in items)
        {
            topLayer = Math.Max(topLayer, item.Layer);
        }

        for (var layer = 0; layer <= topLayer; layer++)
        {
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].Layer == layer)
                {
                    DrawItem(canvas, frame, i, items[i]);
                }
            }
        }
    }

    private void DrawItem(SKCanvas canvas, TimelineFrame frame, int index, TimelineItem item)
    {
        if (item.Fill == TimelineFill.None || !frame.TryGetMarkerBounds(index, out var markerTop, out var markerHeight))
        {
            return;
        }

        var sourceEvent = frame.Events[index];

        var markerColour = GetMarkerColour(frame, sourceEvent, item);

        var markerWidth = Math.Max(item.MinWidth, frame.BandMarkerWidth(item.Band));

        var startX = frame.TimeToX(frame.Times[index]);

        var hasDuration = sourceEvent.DurationUs > 0;

        var endX = hasDuration
            ? frame.TimeToX(frame.Times[index] + sourceEvent.DurationUs / frame.AxisUnitsPerMs)
            : startX + markerWidth;

        if (hasDuration && endX < startX + markerWidth)
        {
            endX = startX + markerWidth;
        }

        if (endX < frame.BandLabelWidth - MaxMarkerWidth || startX > frame.CanvasWidth)
        {
            return;
        }

        if (hasDuration)
        {
            resources.Fill.Color = item.Fill == TimelineFill.Solid
                ? markerColour
                : markerColour.WithAlpha(Math.Min(markerColour.Alpha, DurationOverlayAlpha));

            canvas.DrawRect(startX, markerTop, endX - startX, markerHeight, resources.Fill);
        }

        resources.Fill.Color = markerColour;

        var tickX = item.Tick == TimelineTickAnchor.End && hasDuration ? endX - markerWidth : startX;

        canvas.DrawRect(tickX, markerTop, markerWidth, markerHeight, resources.Fill);

        hitRegions.Add(new HitRegion(new SKRect(startX - HitPad, markerTop, endX + HitPad, markerTop + markerHeight),
                                      sourceEvent,
                                      null));
    }

    private SKColor GetMarkerColour(TimelineFrame frame, EngineEvent sourceEvent, TimelineItem item)
    {
        var colour = item.ColourSource == TimelineColourSource.Provider && frame.ColourProvider is { } colours
            ? colours.GetColour(sourceEvent).ToSkColor()
            : item.Colour;

        return colour.WithAlpha(selection.ShouldDim(sourceEvent) ? DimAlpha : (byte)255);
    }
}
