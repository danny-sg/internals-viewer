using System;
using System.Collections.Generic;
using System.Globalization;
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

    private const byte UnopenedAlpha = 128;

    private const float MinimumRunHeight = 50f;

    private const float RunBandLightness = 0.35f;

    private const float MaximumRunLightness = 0.8f;

    public static List<ColumnstoreRegion> Draw(SKCanvas canvas,
                            SKRect bounds,
                            IReadOnlyList<ScanRowGroup> rowGroups,
                            int? activeRowGroupId,
                            int batchFirstRow,
                            int batchRowCount,
                            SKColor nodeColour,
                            bool isDark)
    {
        canvas.Clear(SKColors.Transparent);

        var regions = new List<ColumnstoreRegion>();

        if (rowGroups.Count == 0)
        {
            return regions;
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

            DrawSegments(canvas, rect, rowGroup, nodeColour, background, regions);

            if (isActive && batchRowCount > 0 && rowGroup.TotalRows > 0)
            {
                DrawBatch(canvas, rect, rowGroup.TotalRows, batchFirstRow, batchRowCount, nodeColour, background);
            }
        }

        return regions;
    }

    private static void DrawSegments(SKCanvas canvas,
                                     SKRect rect,
                                     ScanRowGroup rowGroup,
                                     SKColor nodeColour,
                                     SKPaint paint,
                                     List<ColumnstoreRegion> regions)
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

            DrawRuns(canvas, box, segment, rowGroup, paint.Color, paint);

            regions.Add(Region(segment, rowGroup, box));
        }
    }

    private static void DrawRuns(SKCanvas canvas,
                                 SKRect box,
                                 ScanSegment segment,
                                 ScanRowGroup rowGroup,
                                 SKColor colour,
                                 SKPaint paint)
    {
        if (!segment.IsOpened
            || segment.IsEliminated
            || rowGroup.IsEliminated
            || rowGroup.TotalRows == 0
            || segment.Runs.Count < 2
            || box.Height < MinimumRunHeight)
        {
            return;
        }

        var lines = (int)MathF.Ceiling(box.Height);

        var covering = new int[lines];

        var parity = new int[lines];

        var scale = box.Height / rowGroup.TotalRows;

        var row = 0;

        for (var i = 0; i < segment.Runs.Count; i++)
        {
            var top = row * scale;

            row += segment.Runs[i];

            var bottom = row * scale;

            var first = Math.Clamp((int)MathF.Floor(top), 0, lines - 1);

            var last = Math.Clamp((int)MathF.Ceiling(bottom) - 1, first, lines - 1);

            for (var line = first; line <= last; line++)
            {
                covering[line]++;

                parity[line] = i & 1;
            }
        }

        for (var line = 0; line < lines; line++)
        {
            var lightness = covering[line] switch
            {
                0 => -1f,
                1 => parity[line] == 1 ? RunBandLightness : -1f,
                _ => Density(covering[line])
            };

            if (lightness < 0)
            {
                continue;
            }

            paint.Color = Lighten(colour, lightness);

            canvas.DrawRect(new SKRect(box.Left, box.Top + line, box.Right, box.Top + line + 1), paint);
        }

        paint.Color = colour;
    }

    private static float Density(int runs)
        => Math.Min(MaximumRunLightness, RunBandLightness * (0.5f + (MathF.Log2(runs) / 4f)));

    private static ColumnstoreRegion Region(ScanSegment segment, ScanRowGroup rowGroup, SKRect box)
        => new()
        {
            Bounds = box,
            ElementType = ColumnstoreElementType.Segment,
            Label = segment.ColumnName,
            DetailsFactory = () =>
            [
                new ColumnstoreDetail("Row Group", rowGroup.RowGroupId.ToString(CultureInfo.InvariantCulture)),
                new ColumnstoreDetail("Column", segment.ColumnName),
                new ColumnstoreDetail("Rows", rowGroup.TotalRows.ToString("N0", CultureInfo.InvariantCulture)),
                new ColumnstoreDetail("Runs", segment.Runs.Count.ToString("N0", CultureInfo.InvariantCulture)),
                new ColumnstoreDetail("Eliminated", segment.IsEliminated || rowGroup.IsEliminated ? "Yes" : "No")
            ]
        };

    private static SKColor SegmentColour(ScanSegment segment, ScanRowGroup rowGroup, SKColor nodeColour)
    {
        if (segment.IsEliminated || rowGroup.IsEliminated)
        {
            return ColumnstoreColours.UnknownEncoding;
        }

        return segment.IsOpened ? nodeColour : SKColors.White.WithAlpha(UnopenedAlpha);
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
