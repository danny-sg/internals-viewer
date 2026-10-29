using System;
using System.Linq;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

internal static class TraceIndicatorLayout
{
    public static SKRect GetDefault(SKSize size,
                                    (SKPoint Top, float Bottom)[] anchors,
                                    float rowHeight,
                                    SKRect area,
                                    float gap,
                                    float arrow)
    {
        var centred = SKRect.Create(area.MidX - size.Width / 2, area.MidY - size.Height / 2, size.Width, size.Height);

        var kept = Keep(new SKPoint(centred.Left, centred.Top), size, area);

        if (!anchors.Any(a => Covers(kept, a.Top, a.Bottom)))
        {
            return kept;
        }

        return Place(size, [.. anchors.Select(a => a.Top)], rowHeight, area, gap, arrow).Callout;
    }

    public static bool Covers(SKRect callout, SKPoint top, float bottom)
        => top.X >= callout.Left && top.X <= callout.Right && top.Y <= callout.Bottom && bottom >= callout.Top;

    public static (SKRect Callout, bool IsAbove) Place(SKSize size, SKPoint[] anchors, float rowHeight, SKRect area, float gap, float arrow)
    {
        var centre = anchors.Average(a => a.X);

        var left = Math.Clamp(centre - size.Width / 2, area.Left + gap, Math.Max(area.Left + gap, area.Right - size.Width - gap));

        var above = anchors.Min(a => a.Y) - arrow - size.Height;

        if (above >= area.Top + gap)
        {
            return (SKRect.Create(left, above, size.Width, size.Height), true);
        }

        var below = Math.Min(anchors.Max(a => a.Y) + rowHeight + arrow, area.Bottom - size.Height - gap);

        return (SKRect.Create(left, Math.Max(area.Top + gap, below), size.Width, size.Height), false);
    }

    public static SKRect Keep(SKPoint position, SKSize size, SKRect area)
    {
        var left = Math.Clamp(position.X, area.Left, Math.Max(area.Left, area.Right - size.Width));

        var top = Math.Clamp(position.Y, area.Top, Math.Max(area.Top, area.Bottom - size.Height));

        return SKRect.Create(left, top, size.Width, size.Height);
    }

    public static (SKPoint From, SKPoint To)? GetArrow(SKRect callout, SKPoint top, float bottom, float inset)
    {
        var x = Math.Clamp(top.X, callout.Left + inset, Math.Max(callout.Left + inset, callout.Right - inset));

        if (callout.Bottom <= top.Y)
        {
            return (new SKPoint(x, callout.Bottom), top);
        }

        if (callout.Top >= bottom)
        {
            return (new SKPoint(x, callout.Top), new SKPoint(top.X, bottom));
        }

        var middle = (top.Y + bottom) / 2;

        var y = Math.Clamp(middle, callout.Top + inset, Math.Max(callout.Top + inset, callout.Bottom - inset));

        if (top.X < callout.Left)
        {
            return (new SKPoint(callout.Left, y), new SKPoint(top.X, middle));
        }

        if (top.X > callout.Right)
        {
            return (new SKPoint(callout.Right, y), new SKPoint(top.X, middle));
        }

        return null;
    }
}
