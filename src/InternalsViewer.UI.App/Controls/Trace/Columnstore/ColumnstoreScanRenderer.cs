using System;
using System.Collections.Generic;
using System.Globalization;
using InternalsViewer.UI.App.Controls.Columnstore;
using InternalsViewer.UI.App.Models.Query.Trace.Columnstore;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Trace.Columnstore;

public sealed class ColumnstoreScanRenderer : IDisposable
{
    public const float Margin = 10f;

    public const float RowGroupGap = 14f;

    public const float SegmentGap = 14f;

    public const float SegmentInset = 8f;

    public const float MinimumRowGroupHeight = 14f;

    public const float MinimumBatchHeight = 3f;

    private const double BatchContrast = 0.75;

    private static readonly SKColor ValueRunColour = new(0, 0, 0);

    private static readonly SKColor ReadRunColour = new(80, 80, 80);

    private const byte UnopenedAlpha = 128;

    private const float MinimumRunHeight = 50f;

    private const int RunMinimumLightness = 25;

    private const int RunMaximumLightness = 150;

    private readonly SKPaint _fill = new() { Style = SKPaintStyle.Fill, IsAntialias = true };

    private int[] _runLightness = [];

    public void Dispose() => _fill.Dispose();

    /// <summary>
    /// The height the row groups take, which is the canvas height until they hit their smallest height
    /// </summary>
    public static float GetContentHeight(int rowGroupCount, float viewportHeight)
    {
        if (rowGroupCount == 0)
        {
            return 0;
        }

        var available = viewportHeight - (Margin * 2);

        var height = (available - (RowGroupGap * (rowGroupCount - 1))) / rowGroupCount;

        if (height < MinimumRowGroupHeight)
        {
            height = MinimumRowGroupHeight;
        }

        return (Margin * 2) + (rowGroupCount * height) + (RowGroupGap * (rowGroupCount - 1));
    }

    public List<ColumnstoreRegion> Draw(SKCanvas canvas,
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

        for (var i = 0; i < rowGroups.Count; i++)
        {
            var rowGroup = rowGroups[i];

            var top = area.Top + (i * (height + RowGroupGap));

            var rect = new SKRect(area.Left, top, area.Right, top + height);

            var isActive = activeRowGroupId == rowGroup.RowGroupId;

            _fill.Color = isActive ? Lighten(nodeColour, 0.82f) : panel;

            canvas.DrawRect(rect, _fill);

            DrawSegments(canvas, rect, rowGroup, nodeColour, _fill, regions);

            if (isActive && batchRowCount > 0 && rowGroup.TotalRows > 0)
            {
                DrawBatch(canvas, rect, rowGroup, batchFirstRow, batchRowCount, nodeColour, _fill);
            }

        }

        return regions;
    }

    private void DrawSegments(SKCanvas canvas,
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

    private void DrawRuns(SKCanvas canvas,
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
            || segment.Runs.Count == 0
            || box.Height < MinimumRunHeight)
        {
            return;
        }

        var scale = RunValueScale.Build(segment.Runs, RunMinimumLightness, RunMaximumLightness);

        var lines = (int)MathF.Ceiling(box.Height);

        var lightness = Buffer(ref _runLightness, lines);

        var rowScale = box.Height / rowGroup.TotalRows;

        var row = 0;

        foreach (var run in segment.Runs)
        {
            var top = row * rowScale;

            row += run.Count;

            var first = Math.Clamp((int)MathF.Floor(top), 0, lines - 1);

            var last = Math.Clamp((int)MathF.Ceiling(row * rowScale) - 1, first, lines - 1);

            var value = scale.GetAlpha(run);

            for (var line = first; line <= last; line++)
            {
                lightness[line] = Math.Max(lightness[line], value);
            }
        }

        for (var line = 0; line < lines; line++)
        {
            if (lightness[line] == 0)
            {
                continue;
            }

            paint.Color = Lighten(colour, lightness[line] / 255f);

            canvas.DrawRect(new SKRect(box.Left, box.Top + line, box.Right, box.Top + line + 1), paint);
        }

        paint.Color = colour;
    }

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

    private void DrawBatch(SKCanvas canvas,
                                  SKRect rect,
                                  ScanRowGroup rowGroup,
                                  int firstRow,
                                  int rowCount,
                                  SKColor nodeColour,
                                  SKPaint paint)
    {
        var area = new SKRect(rect.Left + SegmentInset,
                              rect.Top + SegmentInset,
                              rect.Right - SegmentInset,
                              rect.Bottom - SegmentInset);

        var scale = area.Height / rowGroup.TotalRows;

        var top = area.Top + (firstRow * scale);

        var height = Math.Max(MinimumBatchHeight, rowCount * scale);

        if (top + height > area.Bottom)
        {
            top = area.Bottom - height;
        }

        paint.Color = Contrast(nodeColour);

        canvas.DrawRect(new SKRect(area.Left, top, area.Right, top + height), paint);

        if (rowGroup.Segments.Count == 0)
        {
            return;
        }

        var width = (rect.Width - (SegmentInset * 2) - (SegmentGap * (rowGroup.Segments.Count - 1)))
                    / rowGroup.Segments.Count;

        for (var i = 0; i < rowGroup.Segments.Count; i++)
        {
            var segment = rowGroup.Segments[i];

            if (!segment.IsOpened || segment.IsEliminated || rowGroup.IsEliminated)
            {
                continue;
            }

            paint.Color = RunAt(segment, firstRow) switch
            {
                true => ValueRunColour,
                false => ReadRunColour,
                _ => Contrast(nodeColour)
            };

            var left = rect.Left + SegmentInset + (i * (width + SegmentGap));

            canvas.DrawRect(new SKRect(left, top, left + width, top + height), paint);
        }
    }

    private static int[] Buffer(ref int[] buffer, int size)
    {
        if (buffer.Length < size)
        {
            buffer = new int[size];
        }

        Array.Clear(buffer, 0, size);

        return buffer;
    }

    private static bool? RunAt(ScanSegment segment, int row)
    {
        var start = 0;

        foreach (var run in segment.Runs)
        {
            if (run.IsTerminator)
            {
                break;
            }

            if (row < start + run.Count)
            {
                return run.IsPureValue;
            }

            start += run.Count;
        }

        return null;
    }

    private static SKColor Lighten(SKColor colour, float amount)
        => new((byte)(colour.Red + ((255 - colour.Red) * amount)),
               (byte)(colour.Green + ((255 - colour.Green) * amount)),
               (byte)(colour.Blue + ((255 - colour.Blue) * amount)));

    private static SKColor Contrast(SKColor colour)
        => new((byte)Math.Min(255, colour.Red * BatchContrast),
               (byte)Math.Min(255, colour.Green * BatchContrast),
               (byte)Math.Min(255, colour.Blue * BatchContrast));
}
