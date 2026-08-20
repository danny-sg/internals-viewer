using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Metadata.Enums;
using InternalsViewer.UI.App.Models.Columnstore;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Columnstore;

/// <summary>
/// Draws a row per row group with its segments inside, above the row sets that support them
/// </summary>
public sealed class ColumnstoreStructureRenderer
{
    private readonly SKPaint _fill = new() { IsAntialias = false, Style = SKPaintStyle.Fill };

    private readonly SKPathEffect _dots = SKPathEffect.CreateDash([2f, 2f], 0);

    /// <summary>
    /// Text keeps its antialiasing, the shapes and rules being crisper without it
    /// </summary>
    private readonly SKPaint _text = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    /// <summary>
    /// Badges keep their antialiasing, rounded corners needing it where the square boxes do not
    /// </summary>
    private readonly SKPaint _badge = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private readonly SKPaint _stroke = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };

    private readonly SKFont _labelFont = new(GetInterfaceTypeface(SKFontStyleWeight.Normal), 11F)
    {
        Edging = SKFontEdging.SubpixelAntialias,
        Subpixel = true
    };

    private readonly SKFont _titleFont = new(GetInterfaceTypeface(SKFontStyleWeight.SemiBold), 12F);

    /// <summary>
    /// Matches the font the rest of the interface uses, the drawing sitting alongside XAML text
    /// </summary>
    private static SKTypeface GetInterfaceTypeface(SKFontStyleWeight weight)
        => SKTypeface.FromFamilyName(InterfaceFontFamily, weight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
           ?? SKTypeface.Default;

    private const string InterfaceFontFamily = "Segoe UI Variable Text";

    public SKColor TextColour { get; set; } = ColumnstoreColours.Text;

    public SKColor MutedColour { get; set; } = ColumnstoreColours.Muted;

    public SKColor PanelColour { get; set; } = ColumnstoreColours.Panel;

    public SKColor BorderColour { get; set; } = ColumnstoreColours.Border;

    public SKColor SelectionColour { get; set; } = ColumnstoreColours.Selection;

    public SKColor HoverColour { get; set; } = ColumnstoreColours.Hover;

    public ColumnstoreRegion? Selected { get; set; }

    public ColumnstoreRegion? Hover { get; set; }

    public List<ColumnstoreRegion> Draw(SKCanvas canvas,
                                        ColumnStoreIndex index,
                                        IReadOnlyList<RowGroupSummary> rowGroups,
                                        float width,
                                        float scrollOffset)
    {
        var regions = new List<ColumnstoreRegion>();

        canvas.Save();
        canvas.Translate(0, -scrollOffset);

        var y = ColumnstoreLayout.Margin;

        y = DrawHeader(canvas, index, width, y, regions);

        if (rowGroups.Count > 0)
        {
            _text.Color = MutedColour;

            canvas.DrawText("Row Groups",
                            ColumnstoreLayout.Margin + ColumnstoreLayout.ContainerPadding,
                            y + 15,
                            SKTextAlign.Left,
                            _titleFont,
                            _text);

            y += ColumnstoreLayout.ContainerHeaderHeight + ColumnstoreLayout.SectionLabelBottomPadding;
        }

        foreach (var rowGroup in rowGroups)
        {
            DrawRowGroup(canvas, rowGroup, width, y, regions);

            y += ColumnstoreLayout.RowGroupHeight + ColumnstoreLayout.RowGroupGap;
        }

        DrawEmphasis(canvas, regions);

        canvas.Restore();

        return regions;
    }

    /// <summary>
    /// Draws the hover and selection outlines last, so neither is painted over by a later region
    /// </summary>
    private void DrawEmphasis(SKCanvas canvas, List<ColumnstoreRegion> regions)
    {
        if (Hover is { } hover && regions.Contains(hover))
        {
            _stroke.StrokeWidth = 1;

            DrawBorder(canvas, SKRect.Inflate(hover.Bounds, 1, 1), HoverColour);
        }

        if (Selected is { } selected && regions.Contains(selected))
        {
            _stroke.StrokeWidth = 2;

            DrawBorder(canvas, SKRect.Inflate(selected.Bounds, 1, 1), SelectionColour);

            _stroke.StrokeWidth = 1;
        }
    }

    private float DrawHeader(SKCanvas canvas,
                             ColumnStoreIndex index,
                             float width,
                             float y,
                             List<ColumnstoreRegion> regions)
    {
        y = DrawRowSets(canvas, index, width, y, regions);

        return DrawGlobalDictionaries(canvas, index, width, y, regions);
    }

    private float DrawRowSets(SKCanvas canvas,
                              ColumnStoreIndex index,
                              float width,
                              float y,
                              List<ColumnstoreRegion> regions)
    {
        var boxWidth = Math.Min(200f, (width - (ColumnstoreLayout.Margin * 3)) / 2);

        var x = ColumnstoreLayout.Margin;

        var drawn = false;

        if (index.DeleteBitmap is { } deleteBitmap)
        {
            var bounds = new SKRect(x, y, x + boxWidth, y + ColumnstoreLayout.RowSetBoxHeight);

            var detail = deleteBitmap.IsAllocated ? $"{deleteBitmap.FirstPage}" : "not allocated";

            DrawBox(canvas, bounds, ColumnstoreLayout.DeleteBitmapColour, "Delete Bitmap", detail);

            regions.Add(new ColumnstoreRegion
            {
                Bounds = bounds,
                ElementType = ColumnstoreElementType.DeleteBitmap,
                Label = "Delete Bitmap",
                Detail = detail,
                Details =
                [
                    new ColumnstoreDetail("HoBT", deleteBitmap.HobtId.ToString()),
                    new ColumnstoreDetail("Allocation Unit",
                                          deleteBitmap.DataAllocationUnit?.AllocationUnitId.ToString() ?? "none"),
                    new ColumnstoreDetail("First Page", deleteBitmap.FirstPage.ToString()),
                    new ColumnstoreDetail("Root Page", deleteBitmap.RootPage.ToString()),
                    new ColumnstoreDetail("First IAM Page", deleteBitmap.FirstIamPage.ToString())
                ]
            });

            x += boxWidth + ColumnstoreLayout.Margin;

            drawn = true;
        }

        return drawn ? y + ColumnstoreLayout.RowSetBoxHeight + ColumnstoreLayout.Margin : y;
    }

    /// <summary>
    /// A global dictionary is shared by every row group, so it belongs above them rather than in any one
    /// </summary>
    private float DrawGlobalDictionaries(SKCanvas canvas,
                                         ColumnStoreIndex index,
                                         float width,
                                         float y,
                                         List<ColumnstoreRegion> regions)
    {
        var columns = new List<ColumnStoreColumn>();

        foreach (var column in index.Columns)
        {
            if (column.GlobalDictionary is not null)
            {
                columns.Add(column);
            }
        }

        if (columns.Count == 0)
        {
            return y;
        }

        var container = new SKRect(ColumnstoreLayout.Margin,
                                   y,
                                   width - ColumnstoreLayout.Margin,
                                   y + ColumnstoreLayout.GlobalDictionaryContainerHeight);

        _fill.Color = PanelColour;
        canvas.DrawRect(container, _fill);

        DrawBorder(canvas, container, BorderColour);

        _text.Color = MutedColour;
        canvas.DrawText("Global Dictionaries",
                        container.Left + ColumnstoreLayout.ContainerPadding,
                        container.Top + 15,
                        SKTextAlign.Left,
                        _titleFont,
                        _text);

        var available = container.Width - (ColumnstoreLayout.ContainerPadding * 2);

        var blockWidth = ColumnstoreLayout.GetSegmentWidth(available, columns.Count);

        var x = container.Left + ColumnstoreLayout.ContainerPadding;

        y = container.Top + ColumnstoreLayout.ContainerHeaderHeight + ColumnstoreLayout.ContainerPadding;

        foreach (var column in columns)
        {
            var dictionary = column.GlobalDictionary!;

            var bounds = new SKRect(x, y, x + blockWidth, y + ColumnstoreLayout.GlobalDictionaryHeight);

            if (bounds.Right <= container.Right - ColumnstoreLayout.ContainerPadding)
            {
                var dictionaryColour = ColumnstoreLayout.GetDictionaryColour(dictionary.Type);

                _fill.Color = dictionaryColour.WithAlpha(40);
                canvas.DrawRect(bounds, _fill);

                DrawBorder(canvas, bounds, dictionaryColour);

                _text.Color = TextColour;
                canvas.DrawText(Fit(column.Name, bounds.Width - 8, _titleFont),
                                bounds.Left + 4,
                                bounds.Top + 14,
                                SKTextAlign.Left,
                                _titleFont,
                                _text);

                _text.Color = MutedColour;
                canvas.DrawText(Fit($"{dictionary.EntryCount} entries", bounds.Width - 8, _labelFont),
                                bounds.Left + 4,
                                bounds.Top + 27,
                                SKTextAlign.Left,
                                _labelFont,
                                _text);

                regions.Add(new ColumnstoreRegion
                {
                    Bounds = bounds,
                    ElementType = ColumnstoreElementType.Dictionary,
                    Dictionary = dictionary,
                    Label = $"{column.Name} global dictionary",
                    Detail = $"{dictionary.EntryCount} entries",
                    Details = BuildDictionaryDetails(dictionary, true, column.Name)
                });
            }

            x += blockWidth + ColumnstoreLayout.SegmentGap;
        }

        return container.Bottom + ColumnstoreLayout.Margin;
    }

    private void DrawRowGroup(SKCanvas canvas,
                              RowGroupSummary rowGroup,
                              float width,
                              float y,
                              List<ColumnstoreRegion> regions)
    {
        var rowBounds = new SKRect(ColumnstoreLayout.Margin,
                                   y,
                                   width - ColumnstoreLayout.Margin,
                                   y + ColumnstoreLayout.RowGroupHeight);

        _fill.Color = PanelColour;
        canvas.DrawRect(rowBounds, _fill);

        DrawBorder(canvas, rowBounds, BorderColour);

        regions.Add(new ColumnstoreRegion
        {
            Bounds = rowBounds,
            ElementType = ColumnstoreElementType.RowGroup,
            RowGroup = rowGroup,
            Label = $"Row group {rowGroup.RowGroupId}",
            Details =
            [
                new ColumnstoreDetail("State", rowGroup.State.ToString()),
                new ColumnstoreDetail("Rows", $"{rowGroup.TotalRows}"),
                new ColumnstoreDetail("Size", FormatSize(rowGroup.SizeInBytes)),
                new ColumnstoreDetail("Segments", $"{rowGroup.Segments.Count}"),
                new ColumnstoreDetail("Delta Store", rowGroup.DeltaStoreHobtId == 0
                                                         ? "none"
                                                         : rowGroup.DeltaStoreHobtId.ToString())
            ]
        });

        DrawRowGroupMetadata(canvas, rowGroup, rowBounds);

        var segmentsLeft = rowBounds.Left + ColumnstoreLayout.MetadataWidth;

        var available = rowBounds.Right - segmentsLeft - ColumnstoreLayout.SegmentGap;

        if (available <= 0)
        {
            return;
        }

        if (rowGroup.Segments.Count == 0)
        {
            DrawDeltaStore(canvas, rowGroup, segmentsLeft, rowBounds, available, regions);

            return;
        }

        var segmentWidth = ColumnstoreLayout.GetSegmentWidth(available, rowGroup.Segments.Count);

        var x = segmentsLeft;

        foreach (var segment in rowGroup.Segments)
        {
            var bounds = new SKRect(x,
                                    rowBounds.Top + 8,
                                    x + segmentWidth,
                                    rowBounds.Bottom - 8);

            DrawSegment(canvas, segment, bounds, regions);

            x += segmentWidth + ColumnstoreLayout.SegmentGap;
        }
    }

    private void DrawRowGroupMetadata(SKCanvas canvas, RowGroupSummary rowGroup, SKRect rowBounds)
    {
        var x = rowBounds.Left + 10;

        _text.Color = TextColour;
        canvas.DrawText($"Row Group {rowGroup.RowGroupId}",
                        x,
                        rowBounds.Top + 14,
                        SKTextAlign.Left,
                        _titleFont,
                        _text);

        DrawStateBadge(canvas, rowGroup.State, x, rowBounds.Top + 19 + ColumnstoreLayout.BadgeMargin);

        _text.Color = MutedColour;
        canvas.DrawText($"{rowGroup.TotalRows} rows", x, rowBounds.Top + 51, SKTextAlign.Left, _labelFont, _text);
        canvas.DrawText(FormatSize(rowGroup.SizeInBytes), x, rowBounds.Top + 65, SKTextAlign.Left, _labelFont, _text);
    }

    private void DrawStateBadge(SKCanvas canvas, RowGroupState state, float left, float top)
    {
        var (background, foreground) = ColumnstoreLayout.GetStateColours(state);

        var label = state.ToString();

        var metrics = _labelFont.Metrics;

        var height = metrics.Descent - metrics.Ascent + (ColumnstoreLayout.BadgeVerticalPadding * 2);

        var width = _labelFont.MeasureText(label) + (ColumnstoreLayout.BadgePadding * 2);

        var bounds = new SKRect(left, top, left + width, top + height);

        _badge.Color = background;
        canvas.DrawRoundRect(bounds,
                             ColumnstoreLayout.BadgeCornerRadius,
                             ColumnstoreLayout.BadgeCornerRadius,
                             _badge);

        _text.Color = foreground;
        canvas.DrawText(label,
                        bounds.MidX,
                        bounds.Bottom - ColumnstoreLayout.BadgeVerticalPadding - metrics.Descent,
                        SKTextAlign.Center,
                        _labelFont,
                        _text);
    }

    /// <summary>
    /// An open or closed row group holds its rows in a delta store, so it is drawn where its segments would be
    /// </summary>
    private void DrawDeltaStore(SKCanvas canvas,
                                RowGroupSummary rowGroup,
                                float left,
                                SKRect rowBounds,
                                float available,
                                List<ColumnstoreRegion> regions)
    {
        if (rowGroup.DeltaStoreHobtId == 0)
        {
            _text.Color = MutedColour;

            canvas.DrawText(Fit("no segments", available, _labelFont),
                            left + 4,
                            rowBounds.MidY + 4,
                            SKTextAlign.Left,
                            _labelFont,
                            _text);

            return;
        }

        var bounds = new SKRect(left, rowBounds.Top + 8, left + available, rowBounds.Bottom - 8);

        DrawBox(canvas,
                bounds,
                ColumnstoreLayout.DeltaStoreColour,
                "Delta Store",
                $"{rowGroup.TotalRows} rows, hobt {rowGroup.DeltaStoreHobtId}");

        regions.Add(new ColumnstoreRegion
        {
            Bounds = bounds,
            ElementType = ColumnstoreElementType.DeltaStore,
            RowGroup = rowGroup,
            Label = $"Delta Store, Row Group {rowGroup.RowGroupId}",
            Detail = $"{rowGroup.TotalRows} rows",
            Details =
            [
                new ColumnstoreDetail("Row Group", rowGroup.RowGroupId.ToString()),
                new ColumnstoreDetail("State", rowGroup.State.ToString()),
                new ColumnstoreDetail("Rows", $"{rowGroup.TotalRows}"),
                new ColumnstoreDetail("Hobt", rowGroup.DeltaStoreHobtId.ToString())
            ]
        });
    }

    private void DrawSegment(SKCanvas canvas,
                             SegmentSummary segment,
                             SKRect bounds,
                             List<ColumnstoreRegion> regions)
    {
        var colour = ColumnstoreLayout.GetEncodingColour(segment.Encoding);

        _fill.Color = colour.WithAlpha(40);
        canvas.DrawRect(bounds, _fill);

        DrawSizeBar(canvas, segment, bounds, colour);

        DrawBorder(canvas, bounds, colour);

        _text.Color = TextColour;
        canvas.DrawText(Fit(segment.ColumnName, bounds.Width - 8, _labelFont),
                        bounds.Left + 4,
                        bounds.Top + 13,
                        SKTextAlign.Left,
                        _labelFont,
                        _text);

        _text.Color = MutedColour;
        canvas.DrawText(Fit(FormatSize(segment.OnDiskSize), bounds.Width - 8, _labelFont),
                        bounds.Left + 4,
                        bounds.Top + 26,
                        SKTextAlign.Left,
                        _labelFont,
                        _text);

        regions.Add(new ColumnstoreRegion
        {
            Bounds = bounds,
            ElementType = ColumnstoreElementType.Segment,
            Segment = segment,
            Label = segment.ColumnName,
            Detail = $"{segment.EncodingDescription}, {FormatSize(segment.OnDiskSize)}",
            Details =
            [
                new ColumnstoreDetail("Row Group", segment.RowGroupId.ToString()),
                new ColumnstoreDetail("Column", $"{segment.ColumnId}"),
                new ColumnstoreDetail("Encoding", segment.EncodingDescription),
                new ColumnstoreDetail("Rows", $"{segment.RowCount}"),
                new ColumnstoreDetail("Size", FormatSize(segment.OnDiskSize)),
                new ColumnstoreDetail("Bytes Per Row", $"{segment.BytesPerRow:N2}"),
                new ColumnstoreDetail("Min Data Id", $"{segment.MinDataId}"),
                new ColumnstoreDetail("Max Data Id", $"{segment.MaxDataId}"),
                new ColumnstoreDetail("Min Value", segment.MinValueDescription),
                new ColumnstoreDetail("Max Value", segment.MaxValueDescription),
                new ColumnstoreDetail("Dictionary", segment.DictionaryDescription.Length > 0
                                                        ? segment.DictionaryDescription
                                                        : "none"),
                new ColumnstoreDetail("Data Pointer", segment.DataPointerDescription),
                .. GetHeaderDetails(segment)
            ]
        });

        DrawSegmentDictionary(canvas, segment, bounds, regions);
    }

    /// <summary>
    /// A bar along the bottom whose width is the segment's share of the largest segment in the index
    /// </summary>
    private void DrawSizeBar(SKCanvas canvas, SegmentSummary segment, SKRect bounds, SKColor colour)
    {
        var track = new SKRect(bounds.Left,
                               bounds.Bottom - ColumnstoreLayout.SizeBarHeight,
                               bounds.Right,
                               bounds.Bottom);

        _fill.Color = colour.WithAlpha(30);
        canvas.DrawRect(track, _fill);

        var width = (float)(track.Width * segment.SizeFraction);

        if (width <= 0)
        {
            return;
        }

        _fill.Color = colour.WithAlpha(190);
        canvas.DrawRect(new SKRect(track.Left, track.Top, track.Left + width, track.Bottom), _fill);
    }

    /// <summary>
    /// The dictionary a segment reads, drawn inside it, with a dotted border when it is the global one
    /// </summary>
    /// <summary>
    /// What the segment blob's prologue adds, which is nothing until the background read reaches this segment
    /// </summary>
    private static IEnumerable<ColumnstoreDetail> GetHeaderDetails(SegmentSummary segment)
    {
        if (segment.Header is null)
        {
            yield break;
        }

        yield return new ColumnstoreDetail("Structure", segment.StructureDescription);
        yield return new ColumnstoreDetail("RLE Entries", segment.RleDescription);
        yield return new ColumnstoreDetail("Bit Pack Entries", segment.BitPackEntriesDescription);
        yield return new ColumnstoreDetail("Bit Pack Size", segment.BitPackSizeDescription);
        yield return new ColumnstoreDetail("Bookmarks", segment.BookmarkDescription);
    }

    private void DrawSegmentDictionary(SKCanvas canvas,
                                       SegmentSummary segment,
                                       SKRect segmentBounds,
                                       List<ColumnstoreRegion> regions)
    {
        var dictionary = segment.LocalDictionary ?? segment.GlobalDictionary;

        if (dictionary is null)
        {
            return;
        }

        var isGlobal = segment.LocalDictionary is null;

        var bottom = segmentBounds.Bottom - ColumnstoreLayout.SizeBarHeight - 2;

        var right = segmentBounds.Right - 4;

        var left = Math.Max(segmentBounds.Left + 4, right - ColumnstoreLayout.SegmentDictionaryWidth);

        var bounds = new SKRect(left, bottom - ColumnstoreLayout.DictionaryHeight, right, bottom);

        if (bounds.Width <= 0)
        {
            return;
        }

        var colour = ColumnstoreLayout.GetDictionaryColour(dictionary.Type);

        _fill.Color = colour.WithAlpha(isGlobal ? (byte)40 : (byte)210);
        canvas.DrawRect(bounds, _fill);

        DrawBorder(canvas, bounds, colour, isGlobal);

        _text.Color = isGlobal ? TextColour : SKColors.White;

        var label = isGlobal ? "Global" : $"Dict {dictionary.EntryCount}";

        canvas.DrawText(Fit(label, bounds.Width - 6, _labelFont),
                        bounds.MidX,
                        bounds.Bottom - 3,
                        SKTextAlign.Center,
                        _labelFont,
                        _text);

        regions.Add(new ColumnstoreRegion
        {
            Bounds = bounds,
            ElementType = ColumnstoreElementType.Dictionary,
            Segment = segment,
            Dictionary = dictionary,
            Label = isGlobal ? "Global Dictionary" : "Local Dictionary",
            Detail = $"{dictionary.EntryCount} entries",
            Details = BuildDictionaryDetails(dictionary, isGlobal, segment.ColumnName)
        });
    }

    private static List<ColumnstoreDetail> BuildDictionaryDetails(SegmentDictionary dictionary,
                                                                  bool isGlobal,
                                                                  string columnName) =>
    [
        new("Scope", isGlobal ? "Global, one per column" : "Local to this segment"),
        new("Type", ColumnstoreLayout.GetDictionaryTypeDescription(dictionary.Type)),
        new("Column", columnName),
        new("Dictionary", dictionary.DictionaryId.ToString()),
        new("Entries", $"{dictionary.EntryCount}"),
        new("First Data Id", $"{dictionary.LastId - dictionary.EntryCount + 1}"),
        new("Size", FormatSize(dictionary.OnDiskSize))
    ];

    private void DrawBox(SKCanvas canvas, SKRect bounds, SKColor colour, string title, string detail)
    {
        _fill.Color = colour.WithAlpha(40);
        canvas.DrawRect(bounds, _fill);

        DrawBorder(canvas, bounds, colour);

        _text.Color = TextColour;
        canvas.DrawText(title, bounds.Left + 8, bounds.Top + 18, SKTextAlign.Left, _titleFont, _text);

        _text.Color = MutedColour;
        canvas.DrawText(Fit(detail, bounds.Width - 16, _labelFont),
                        bounds.Left + 8,
                        bounds.Top + 33,
                        SKTextAlign.Left,
                        _labelFont,
                        _text);
    }

    /// <summary>
    /// Strokes a one pixel border, inset by half a pixel so it lands on the pixel rather than across two
    /// </summary>
    private void DrawBorder(SKCanvas canvas, SKRect bounds, SKColor colour, bool isDotted = false)
    {
        _stroke.Color = colour;
        _stroke.PathEffect = isDotted ? _dots : null;

        canvas.DrawRect(SKRect.Inflate(bounds, -0.5f, -0.5f), _stroke);

        _stroke.PathEffect = null;
    }

    /// <summary>
    /// Text that fits the space, or nothing at all - a truncated label reads as a different value
    /// </summary>
    /// <remarks>
    /// What is dropped is still on the hover tooltip, so the drawing stays legible without losing detail.
    /// </remarks>
    private string Fit(string text, float width, SKFont font)
        => width > 0 && font.MeasureText(text) <= width ? text : string.Empty;

    public static string FormatSize(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / 1024d / 1024d:N1} MB",
        >= 1024 => $"{bytes / 1024d:N1} KB",
        _ => $"{bytes} B"
    };

    public void Dispose()
    {
        _fill.Dispose();
        _text.Dispose();
        _badge.Dispose();
        _stroke.Dispose();
        _labelFont.Dispose();
        _dots.Dispose();
        _titleFont.Dispose();
    }
}
