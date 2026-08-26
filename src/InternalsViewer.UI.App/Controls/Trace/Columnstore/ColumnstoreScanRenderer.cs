using System;
using System.Collections.Generic;
using InternalsViewer.UI.App.Controls.Columnstore;
using InternalsViewer.UI.App.Models.Query.Trace.Columnstore;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Trace.Columnstore;

public static class ColumnstoreScanRenderer
{
    public const float Margin = 10f;

    public const float RowGroupGap = 14f;

    public const float SegmentGap = 14f;

    public const float SegmentInset = 8f;

    public const float MinimumRowGroupHeight = 14f;

    public const float MinimumBatchHeight = 3f;

    private const byte UnselectedAlpha = 128;

    public static void Draw(SKCanvas canvas,
                            SKRect bounds,
                            IReadOnlyList<ScanRowGroup> rowGroups,
                            int? activeRowGroupId,
                            int batchFirstRow,
                            int batchRowCount,
                            SKColor nodeColour,
                            bool isDark)
    {
        canvas.Clear(SKColors.Transparent);

        if (rowGroups.Count == 0)
        {
            return;
        }

        var area = new SKRect(bounds.Left + Margin, bounds.Top + Margin, bounds.Right - Margin, bounds.Bottom - Margin);

        var height = (area.Height - (RowGroupGap * (rowGroups.Count - 1))) / rowGroups.Count;

        if (height < MinimumRowGroupHeight)
        {
            height = MinimumRowGroupHeight;
        }

        var panel = isDark ? ColumnstoreColours.DarkPanel : ColumnstoreColours.Panel;

        using var background = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };

        for (var i = 0; i < rowGroups.Count; i++)
        {
            var rowGroup = rowGroups[i];

            var top = area.Top + (i * (height + RowGroupGap));

            var rect = new SKRect(area.Left, top, area.Right, top + height);

            var isActive = activeRowGroupId == rowGroup.RowGroupId;

            background.Color = isActive ? Lighten(nodeColour, 0.82f) : panel;

            canvas.DrawRect(rect, background);

            DrawSegments(canvas, rect, rowGroup, nodeColour, background);

            if (isActive && batchRowCount > 0 && rowGroup.TotalRows > 0)
            {
                DrawBatch(canvas, rect, rowGroup.TotalRows, batchFirstRow, batchRowCount, nodeColour, background);
            }
        }
    }

    private static void DrawSegments(SKCanvas canvas,
                                     SKRect rect,
                                     ScanRowGroup rowGroup,
                                     SKColor nodeColour,
                                     SKPaint paint)
    {
        if (rowGroup.Segments.Count == 0)
        {
            return;
        }

        var width = (rect.Width - (SegmentInset * 2) - (SegmentGap * (rowGroup.Segments.Count - 1)))
                    / rowGroup.Segments.Count;

        for (var i = 0; i < rowGroup.Segments.Count; i++)
        {
            var segment = rowGroup.Segments[i];

            var left = rect.Left + SegmentInset + (i * (width + SegmentGap));

            var box = new SKRect(left, rect.Top + SegmentInset, left + width, rect.Bottom - SegmentInset);

            paint.Color = SegmentColour(segment, rowGroup, nodeColour);

            canvas.DrawRect(box, paint);
        }
    }

    private static SKColor SegmentColour(ScanSegment segment, ScanRowGroup rowGroup, SKColor nodeColour)
    {
        if (segment.IsEliminated || rowGroup.IsEliminated)
        {
            return ColumnstoreColours.UnknownEncoding;
        }

        return segment.IsProjected ? nodeColour : SKColors.White.WithAlpha(UnselectedAlpha);
    }

    private static void DrawBatch(SKCanvas canvas,
                                  SKRect rect,
                                  int totalRows,
                                  int firstRow,
                                  int rowCount,
                                  SKColor nodeColour,
                                  SKPaint paint)
    {
        var area = new SKRect(rect.Left + SegmentInset,
                              rect.Top + SegmentInset,
                              rect.Right - SegmentInset,
                              rect.Bottom - SegmentInset);

        var scale = area.Height / totalRows;

        var top = area.Top + (firstRow * scale);

        var height = Math.Max(MinimumBatchHeight, rowCount * scale);

        if (top + height > area.Bottom)
        {
            top = area.Bottom - height;
        }

        paint.Color = Contrast(nodeColour);

        canvas.DrawRect(new SKRect(area.Left, top, area.Right, top + height), paint);
    }

    private static SKColor Lighten(SKColor colour, float amount)
        => new((byte)(colour.Red + ((255 - colour.Red) * amount)),
               (byte)(colour.Green + ((255 - colour.Green) * amount)),
               (byte)(colour.Blue + ((255 - colour.Blue) * amount)));

    private static SKColor Contrast(SKColor colour)
        => new((byte)Math.Min(255, colour.Red * 0.45),
               (byte)Math.Min(255, colour.Green * 0.45),
               (byte)Math.Min(255, colour.Blue * 0.45));
}
