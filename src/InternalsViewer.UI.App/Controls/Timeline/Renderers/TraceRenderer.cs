using System;
using System.Collections.Generic;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Timeline.Definition;
using InternalsViewer.UI.App.Helpers;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline.Renderers;

/// <summary>
/// Draws the I/O and log trace extensions: faint rails that connect an operator bar to the reads it issued (below) and
/// the log records it wrote (above)
/// </summary>
/// <remarks>
/// Reads are modelled as a Volcano call/return â€” a dotted call rail to the top of the read, then one solid return rail
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

    private const byte RailAlpha = 150;

    public void Draw(SKCanvas canvas, TimelineFrame frame, IReadOnlyList<OperatorBar> bars)
    {
        var links = frame.Definition.Links;

        if (links.Length == 0)
        {
            return;
        }

        var byNode = new Dictionary<PlanNodeIdentifier, OperatorBar>(bars.Count);

        foreach (var b in bars)
        {
            if (b.Op.PlanNodeIdentifier is { } id)
            {
                byNode[id] = b;
            }
        }

        var rightEdge = frame.CanvasWidth;

        // Composite all extensions through a single layer at reduced opacity so overlapping traces don't stack up to
        // full opacity (the layer merges them first, then fades the whole thing once). Bound the layer to just the rows
        // traces reach — the operator bars plus the read/log lanes — so Skia allocates a band-sized offscreen rather
        // than a full-canvas one on every playback (the cost otherwise scales with the whole control's size).
        canvas.SaveLayer(TraceBounds(frame, bars, rightEdge), resources.TraceLayer);

        foreach (var link in links)
        {
            if (!frame.TryGetTrackBounds(link.Source, out var sourceTop, out var sourceHeight))
            {
                continue;
            }

            var sourceBottom = sourceTop + sourceHeight;

            if (!TryGetOrigin(frame, byNode, link, sourceTop, sourceBottom, out var origin))
            {
                continue;
            }

            var item = frame.Definition.Items[link.Source];

            var sourceEvent = frame.Definition.Events[link.Source];

            var width = frame.BandMarkerWidth(item.Band);

            var startX = frame.TimeToX(frame.Times[link.Source]);

            var endX = sourceEvent.DurationUs > 0
                ? frame.TimeToX(frame.Times[link.Source] + sourceEvent.DurationUs / frame.AxisUnitsPerMs)
                : startX;

            if (startX > rightEdge || endX < frame.BandLabelWidth - width)
            {
                continue;
            }

            var colour = frame.BaseColour(link.Source).WithAlpha(selection.ShouldDim(sourceEvent) ? DimAlpha : RailAlpha);

            var isAbove = origin < sourceTop;

            var near = isAbove ? sourceTop : sourceBottom;

            if (link.Style == TimelineLinkStyle.Bar)
            {
                resources.Fill.Color = colour;
                canvas.DrawRect(startX, Math.Min(near, origin), width, Math.Abs(origin - near), resources.Fill);

                continue;
            }

            var far = isAbove ? sourceBottom : sourceTop;

            // Only draw the dotted call rail when it is far enough left of the end return rail to read as distinct.
            if (endX - startX > MinCallRailGapPx)
            {
                resources.ReadCallRail.Color = colour;
                canvas.DrawLine(startX, origin, startX, near, resources.ReadCallRail);
            }

            resources.ReadReturnRail.Color = colour;

            // The pages land in the buffer together when the I/O completes, so — absent real per-page timing — the
            // return rails bunch at the read's END, only slightly separated for legibility. A single-page read is
            // therefore one rail at the end.
            for (var p = 0; p < link.RailCount; p++)
            {
                var x = Math.Max(startX, endX - p * PageRailGapPx);

                canvas.DrawLine(x, origin, x, far, resources.ReadReturnRail);
            }
        }

        canvas.Restore();
    }

    private static bool TryGetOrigin(TimelineFrame frame,
                                     Dictionary<PlanNodeIdentifier, OperatorBar> byNode,
                                     TimelineLink link,
                                     float sourceTop,
                                     float sourceBottom,
                                     out float origin)
    {
        if (link.TargetItem >= 0
            && frame.TryGetMarkerBounds(link.TargetItem, out var targetTop, out var targetHeight)
            && TryGetEdge(targetTop, targetTop + targetHeight, sourceTop, sourceBottom, out origin))
        {
            return true;
        }

        if (link.TargetOperator is { } id
            && byNode.TryGetValue(id, out var bar)
            && TryGetEdge(bar.BarTop, bar.BarBottom, sourceTop, sourceBottom, out origin))
        {
            return true;
        }

        origin = 0;

        return false;
    }

    private static bool TryGetEdge(float targetTop, float targetBottom, float sourceTop, float sourceBottom, out float edge)
    {
        if (targetBottom < sourceTop)
        {
            edge = targetBottom;

            return true;
        }

        if (targetTop > sourceBottom)
        {
            edge = targetTop;

            return true;
        }

        edge = 0;

        return false;
    }

    // The vertical span the rails occupy: the operator bars they drop from, plus the read and log lanes they reach.
    // Used to size the composite layer's offscreen to the band instead of the whole canvas.
    private static SKRect TraceBounds(TimelineFrame frame, IReadOnlyList<OperatorBar> bars, float rightEdge)
    {
        var top = float.MaxValue;
        var bottom = float.MinValue;

        foreach (var b in bars)
        {
            top = Math.Min(top, b.BarTop);

            bottom = Math.Max(bottom, b.BarBottom);
        }

        foreach (var link in frame.Definition.Links)
        {
            IncludeBand(frame, link.Source, ref top, ref bottom);

            IncludeBand(frame, link.TargetItem, ref top, ref bottom);
        }

        return new SKRect(frame.BandLabelWidth, top, rightEdge, bottom);
    }

    private static void IncludeBand(TimelineFrame frame, int itemIndex, ref float top, ref float bottom)
    {
        if (itemIndex < 0)
        {
            return;
        }

        var band = frame.Definition.Items[itemIndex].Band;

        if (band < 0)
        {
            return;
        }

        top = Math.Min(top, frame.BandTops[band]);

        bottom = Math.Max(bottom, frame.BandTops[band] + frame.BandHeights[band]);
    }
}
