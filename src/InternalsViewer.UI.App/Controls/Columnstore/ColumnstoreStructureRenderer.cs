using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Columnstore.Metadata.Enums;
using InternalsViewer.Internals.Helpers;
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

    private readonly SKPaint _stroke = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 0 };

    private readonly SKFont _labelFont = new(GetInterfaceTypeface(SKFontStyleWeight.Normal), 12F)
    {
        Edging = SKFontEdging.SubpixelAntialias,
        Subpixel = true
    };

    private readonly SKFont _titleFont = new(GetInterfaceTypeface(SKFontStyleWeight.SemiBold), 12F);

    /// <summary>
    /// Badges in the drawing sit below the size the command bars use, having far less room to take
    /// </summary>
    private readonly SKFont _badgeFont = new(GetInterfaceTypeface(SKFontStyleWeight.Normal), 9F)
    {
        Edging = SKFontEdging.SubpixelAntialias,
        Subpixel = true
    };

    private readonly SKRoundRect _roundRect = new();

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

    /// <summary>
    /// Coding of each dictionary's pages, which arrives after the drawing is first painted
    /// </summary>
    public IReadOnlyDictionary<long, SubLobType> DictionaryCoding { get; set; } = new Dictionary<long, SubLobType>();

    public static long CodingKey(int columnId, int dictionaryId) => ((long)columnId << 32) | (uint)dictionaryId;

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

            var detail = deleteBitmap.IsAllocated ? $"{deleteBitmap.FirstPage}" : "(Not allocated)";

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
        var hasDictionary = false;

        foreach (var column in index.Columns)
        {
            hasDictionary |= column.GlobalDictionary is not null;
        }

        if (!hasDictionary)
        {
            return y;
        }

        var container = new SKRect(ColumnstoreLayout.Margin,
                                   y,
                                   width - ColumnstoreLayout.Margin,
                                   y + ColumnstoreLayout.GlobalDictionaryContainerHeight);

        _fill.Color = PanelColour;
        canvas.DrawRect(container, _fill);

        _text.Color = MutedColour;
        canvas.DrawText("Global Dictionaries",
                        container.Left + ColumnstoreLayout.ContainerPadding,
                        container.MidY + 4,
                        SKTextAlign.Left,
                        _titleFont,
                        _text);

        // Laid out on the segment grid rather than packed up, so a dictionary sits over the column it belongs to
        var blockWidth = ColumnstoreLayout.GetSegmentWidth(ColumnstoreLayout.GetSegmentsAvailable(width),
                                                           index.Columns.Count);

        var x = ColumnstoreLayout.GetSegmentsLeft();

        y = container.Top + ColumnstoreLayout.ContainerPadding;

        foreach (var column in index.Columns)
        {
            var bounds = new SKRect(x, y, x + blockWidth, y + ColumnstoreLayout.GlobalDictionaryHeight);

            x += blockWidth + ColumnstoreLayout.SegmentGap;

            if (column.GlobalDictionary is not { } dictionary)
            {
                continue;
            }

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

                var entries = $"{dictionary.EntryCount} entries";

                var entriesWidth = _labelFont.MeasureText(entries);

                _text.Color = MutedColour;

                canvas.DrawText(Fit(entries, bounds.Width - 8, _labelFont),
                                bounds.Left + 4,
                                bounds.Top + 27,
                                SKTextAlign.Left,
                                _labelFont,
                                _text);

                DrawBadges(canvas,
                           DictionaryBadges(dictionary),
                           bounds.Left + 8 + entriesWidth,
                           bounds.Top + 27 - BadgeHeight + _labelFont.Metrics.Descent,
                           bounds.Width - 12 - entriesWidth,
                           0);

                regions.Add(new ColumnstoreRegion
                {
                    Bounds = bounds,
                    ElementType = ColumnstoreElementType.Dictionary,
                    Dictionary = dictionary,
                    Label = $"{column.Name} Global Dictionary",
                    Detail = $"{dictionary.EntryCount} entries",
                    Details = BuildDictionaryDetails(dictionary, true, column.Name)
                });
            }
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

        regions.Add(new ColumnstoreRegion
        {
            Bounds = rowBounds,
            ElementType = ColumnstoreElementType.RowGroup,
            RowGroup = rowGroup,
            Label = $"Row Group {rowGroup.RowGroupId}",
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

        var segmentsLeft = ColumnstoreLayout.GetSegmentsLeft();

        var available = ColumnstoreLayout.GetSegmentsAvailable(width);

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
                        rowBounds.Top + 18,
                        SKTextAlign.Left,
                        _titleFont,
                        _text);

        DrawStateBadge(canvas, rowGroup.State, x, rowBounds.Top + 24 + ColumnstoreLayout.BadgeMargin);

        _text.Color = MutedColour;

        canvas.DrawText($"{rowGroup.TotalRows} rows", x, rowBounds.Bottom - 8, SKTextAlign.Left, _labelFont, _text);
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

            canvas.DrawText(Fit("No segments", available, _labelFont),
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

        _fill.Color = colour.WithAlpha(20);

        canvas.DrawRect(bounds, _fill);

        DrawSizeBar(canvas, segment, bounds, colour, regions);

        DrawBorder(canvas, bounds, colour);

        var contentLeft = bounds.Left + ColumnstoreLayout.SizeBarWidth;

        var available = bounds.Right - contentLeft - 8;

        // The name is what identifies the segment, so it takes the room it needs and the badge takes what is left
        var name = Fit(segment.ColumnName, available, _titleFont);

        _text.Color = TextColour;

        canvas.DrawText(name,
                        contentLeft + 4,
                        bounds.Top + 15,
                        SKTextAlign.Left,
                        _titleFont,
                        _text);

        var nameWidth = name.Length == 0 ? 0 : _titleFont.MeasureText(name) + TitleBadgeGap;

        DrawSegmentBadges(canvas, segment, bounds, contentLeft, available - nameWidth);

        var regionBounds = new SKRect(contentLeft,
                                      bounds.Top,
                                      bounds.Right,
                                      bounds.Bottom);

        regions.Add(new ColumnstoreRegion
        {
            Bounds = regionBounds,
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
                new ColumnstoreDetail("Data Pointer", segment.DataPointerDescription),
            ]
        });

        DrawSegmentDictionary(canvas, segment, bounds, contentLeft, regions);
    }

    /// <summary>
    /// A bar down the left whose height is the segment's share of the largest segment in the index
    /// </summary>
    private void DrawSizeBar(SKCanvas canvas, SegmentSummary segment, SKRect bounds, SKColor colour, List<ColumnstoreRegion> regions)
    {
        var track = new SKRect(bounds.Left,
                               bounds.Top,
                               bounds.Left + ColumnstoreLayout.SizeBarWidth,
                               bounds.Bottom);

        _fill.Color = colour.WithAlpha(30);

        canvas.DrawRect(track, _fill);

        var height = (float)(track.Height * segment.SizeFraction);

        if (height <= 0)
        {
            return;
        }

        _fill.Color = colour.WithAlpha(190);

        canvas.DrawRect(new SKRect(track.Left, track.Bottom - height, track.Right, track.Bottom), _fill);

        regions.Add(new ColumnstoreRegion
        {
            Bounds = bounds,
            ElementType = ColumnstoreElementType.Segment,
            Segment = segment,
            Label = segment.ColumnName,
            Detail = $"{segment.EncodingDescription}, {FormatSize(segment.OnDiskSize)}",
            Details =
            [
                new ColumnstoreDetail("Size", FormatSize(segment.OnDiskSize)),
                new ColumnstoreDetail("Compression", (1D - segment.SizeFraction).ToString("P2")),
            ]
        });

    }


    /// <summary>
    /// Width a run of badges needs, which decides whether it is drawn at all
    /// </summary>
    private float MeasureBadges(IReadOnlyList<(string Label, SKColor Colour)> badges, float gap)
    {
        var width = gap * (badges.Count - 1);

        foreach (var badge in badges)
        {
            width += _badgeFont.MeasureText(badge.Label) + (ColumnstoreLayout.BadgePadding * 2);
        }

        return width;
    }

    private float BadgeHeight
        => _badgeFont.Metrics.Descent - _badgeFont.Metrics.Ascent + (ColumnstoreLayout.BadgeVerticalPadding * 2);

    /// <summary>
    /// Draws a run of badges, or none of it when the run will not fit
    /// </summary>
    /// <remarks>
    /// A flag badge means the segment carries that thing, so a run cut short reads as flags the segment does not
    /// have. The whole run is dropped rather than trimmed, the same as a label that will not fit.
    /// </remarks>
    private bool DrawBadges(SKCanvas canvas,
                            IReadOnlyList<(string Label, SKColor Colour)> badges,
                            float left,
                            float top,
                            float available,
                            float gap)
    {
        if (badges.Count == 0 || MeasureBadges(badges, gap) > available)
        {
            return false;
        }

        var x = left;

        for (var i = 0; i < badges.Count; i++)
        {
            var width = _badgeFont.MeasureText(badges[i].Label) + (ColumnstoreLayout.BadgePadding * 2);

            var isCompound = gap <= 0;

            DrawBadge(canvas,
                      new SKRect(x, top, x + width, top + BadgeHeight),
                      badges[i].Label,
                      badges[i].Colour,
                      !isCompound || i == 0,
                      !isCompound || i == badges.Count - 1);

            x += width + gap;
        }

        return true;
    }

    /// <summary>
    /// Rounded on the outer edges only, so a run drawn without gaps reads as one compound chip
    /// </summary>
    private void DrawBadge(SKCanvas canvas, SKRect bounds, string label, SKColor colour, bool roundStart, bool roundEnd)
    {
        var start = roundStart ? ColumnstoreLayout.BadgeCornerRadius : 0;

        var end = roundEnd ? ColumnstoreLayout.BadgeCornerRadius : 0;

        _roundRect.SetRectRadii(bounds,
        [
            new SKPoint(start, start),
            new SKPoint(end, end),
            new SKPoint(end, end),
            new SKPoint(start, start)
        ]);

        _badge.Color = colour;

        canvas.DrawRoundRect(_roundRect, _badge);

        _text.Color = SKColors.White;

        canvas.DrawText(label,
                        bounds.MidX,
                        bounds.Bottom - ColumnstoreLayout.BadgeVerticalPadding - _badgeFont.Metrics.Descent,
                        SKTextAlign.Center,
                        _badgeFont,
                        _text);
    }

    /// <summary>
    /// How the column was compressed, and what the prologue turned out to hold once it had been read
    /// </summary>
    private void DrawSegmentBadges(SKCanvas canvas,
                                   SegmentSummary segment,
                                   SKRect bounds,
                                   float contentLeft,
                                   float encodingAvailable)
    {
        var left = contentLeft + 4;

        var available = bounds.Right - left - 4;

        var encoding = new[] { (segment.EncodingDescription, ColumnstoreLayout.GetEncodingColour(segment.Encoding)) };

        var encodingWidth = MeasureBadges(encoding, 0);

        if (encodingWidth <= encodingAvailable)
        {
            DrawBadges(canvas,
                       encoding,
                       bounds.Right - 4 - encodingWidth,
                       bounds.Top + BadgeTopMargin,
                       encodingWidth,
                       0);
        }

        var top = bounds.Top + ColumnstoreLayout.LabelHeight + ColumnstoreLayout.BadgeMargin + BadgeTopMargin;

        if (segment.Header is not { } header)
        {
            return;
        }

        var flags = new List<(string, SKColor)>
        {
            (header.StructureType.ToString().SplitCamelCase(), ColumnstoreLayout.GetStructureColour(header.StructureType))
        };

        if (header.HasBitpackArray)
        {
            flags.Add(("Bit Pack", ColumnstoreColours.BitPackFlag));
        }

        if (header.IsStoreByValue)
        {
            flags.Add(("Value Store", ColumnstoreColours.ValueStoreFlag));
        }

        if (top + BadgeHeight <= bounds.Bottom - ColumnstoreLayout.DictionaryHeight)
        {
            DrawBadges(canvas, flags, left, top, available, 0);
        }
    }

    private void DrawSegmentDictionary(SKCanvas canvas,
                                       SegmentSummary segment,
                                       SKRect segmentBounds,
                                       float contentLeft,
                                       List<ColumnstoreRegion> regions)
    {
        var dictionary = segment.LocalDictionary ?? segment.GlobalDictionary;

        if (dictionary is null)
        {
            return;
        }

        var isGlobal = segment.LocalDictionary is null;

        if (GetDictionaryBounds(segmentBounds, contentLeft) is not { } bounds)
        {
            return;
        }

        var colour = ColumnstoreLayout.GetDictionaryColour(dictionary.Type);

        _fill.Color = colour.WithAlpha(isGlobal ? (byte)40 : (byte)210);
        canvas.DrawRect(bounds, _fill);

        DrawBorder(canvas, bounds, colour, isGlobal);

        _text.Color = isGlobal ? TextColour : SKColors.White;

        var label = isGlobal ? "Global" : $"Dictionary {dictionary.EntryCount}";

        canvas.DrawText(Fit(label, bounds.Width - 6, _badgeFont),
                        bounds.MidX,
                        bounds.Bottom - ColumnstoreLayout.BadgeVerticalPadding - _badgeFont.Metrics.Descent,
                        SKTextAlign.Center,
                        _badgeFont,
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

    /// <summary>
    /// What the metadata says the dictionary is, which is all that is known without reading its blob
    /// </summary>
    private IReadOnlyList<(string Label, SKColor Colour)> DictionaryBadges(SegmentDictionary dictionary)
    {
        var badges = new List<(string, SKColor)>
        {
            (ColumnstoreLayout.GetDictionaryTypeDescription(dictionary.Type),
             ColumnstoreLayout.GetDictionaryColour(dictionary.Type))
        };

        if (DictionaryCoding.TryGetValue(CodingKey(dictionary.ColumnId, dictionary.DictionaryId), out var coding)
            && coding == SubLobType.CompressedStringPage)
        {
            badges.Add(("Huffman", ColumnstoreColours.HuffmanFlag));
        }

        return badges;
    }

    private const float BadgeTopMargin = 6f;

    private const float TitleBadgeGap = 6f;

    /// <summary>
    /// The dictionary block, which sits at the bottom right of the segment, clear of its size bar
    /// </summary>
    private static SKRect? GetDictionaryBounds(SKRect segmentBounds, float contentLeft)
    {
        var right = segmentBounds.Right - 4;

        var left = Math.Max(contentLeft + 4, right - ColumnstoreLayout.SegmentDictionaryWidth);

        var bottom = segmentBounds.Bottom - 3;

        var bounds = new SKRect(left, bottom - ColumnstoreLayout.DictionaryHeight, right, bottom);

        return bounds.Width > 0 ? bounds : null;
    }

    private static List<ColumnstoreDetail> BuildDictionaryDetails(SegmentDictionary dictionary,
                                                                  bool isGlobal,
                                                                  string columnName) =>
    [
        new("Scope", isGlobal ? "Global" : "Local"),
        new("Type", ColumnstoreLayout.GetDictionaryTypeDescription(dictionary.Type)),
        new("Column", columnName),
        new("Dictionary", dictionary.DictionaryId.ToString()),
        new("Entries", $"{dictionary.EntryCount}"),
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
    private static string Fit(string text, float width, SKFont font)
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
        _badgeFont.Dispose();
        _roundRect.Dispose();
        _dots.Dispose();
        _titleFont.Dispose();
    }
}
