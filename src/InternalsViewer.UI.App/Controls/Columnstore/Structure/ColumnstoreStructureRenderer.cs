using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Columnstore.Metadata.Enums;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Columnstore;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Columnstore.Structure;

/// <summary>
/// Draws a row per row group with its segments inside, above the row sets that support them
/// </summary>
public sealed class ColumnstoreStructureRenderer : IDisposable
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

    private readonly SKFont _labelFont = new(InterfaceTypeface, 12F)
    {
        Edging = SKFontEdging.SubpixelAntialias,
        Subpixel = true
    };

    private readonly SKFont _titleFont = new(InterfaceSemiBoldTypeface, 12F);

    /// <summary>
    /// Badges in the drawing sit below the size the command bars use, having far less room to take
    /// </summary>
    private readonly SKFont _badgeFont = new(InterfaceTypeface, 9F)
    {
        Edging = SKFontEdging.SubpixelAntialias,
        Subpixel = true
    };

    /// <summary>
    /// Types read as code, so they are set in the same face the editor uses rather than the interface one
    /// </summary>
    private readonly SKFont _monoFont = new(MonoTypeface, 12F)
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

    private static readonly SKTypeface InterfaceTypeface = GetInterfaceTypeface(SKFontStyleWeight.Normal);

    private static readonly SKTypeface InterfaceSemiBoldTypeface = GetInterfaceTypeface(SKFontStyleWeight.SemiBold);

    private static readonly SKTypeface MonoTypeface = SKTypeface.FromFamilyName("Cascadia Mono") ?? SKTypeface.Default;

    private const string InterfaceFontFamily = "Segoe UI Variable Text";

    public SKColor TextColour { get; set; } = ColumnstoreColours.Text;

    public SKColor MutedColour { get; set; } = ColumnstoreColours.Muted;

    public SKColor PanelColour { get; set; } = ColumnstoreColours.Panel;

    public SKColor BandColour { get; set; } = ColumnstoreColours.Panel;

    public SKColor HoverBandColour { get; set; } = ColumnstoreColours.Hover;

    public SKColor LocatorBandColour { get; set; } = ColumnstoreColours.LocatorBand;

    public SKColor KeywordColour { get; set; } = ColumnstoreColours.Text;

    public SKColor NumberColour { get; set; } = ColumnstoreColours.Text;

    public SKColor PunctuationColour { get; set; } = ColumnstoreColours.Muted;
    
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

        var afterRowSets = DrawRowSets(canvas, index, width, y, regions);

        // The row sets belong to the index rather than to any column, so a rule sets them apart from the grid
        y = afterRowSets > y
            ? DrawSeparator(canvas, width, afterRowSets - ColumnstoreLayout.Margin)
            : afterRowSets;

        var hasLocalDictionaries = false;

        var hasGlobalDictionaries = false;

        foreach (var rowGroup in rowGroups)
        {
            foreach (var segment in rowGroup.Segments)
            {
                hasLocalDictionaries |= segment.LocalDictionary is not null;
            }
        }

        foreach (var column in index.Columns)
        {
            hasGlobalDictionaries |= column.GlobalDictionary is not null;
        }

        var columnHeaderHeight = GetColumnHeaderHeight(index, width);

        DrawColumnBands(canvas,
                        index,
                        rowGroups,
                        width,
                        y,
                        columnHeaderHeight,
                        hasGlobalDictionaries,
                        hasLocalDictionaries);

        if (index.Columns.Count > 0)
        {
            DrawColumnHeaders(canvas, index, width, y);

            y += columnHeaderHeight + ColumnstoreLayout.SectionLabelBottomPadding;
        }

        y = DrawGlobalDictionaries(canvas, index, width, y, regions);

        foreach (var rowGroup in rowGroups)
        {
            DrawRowGroup(canvas, rowGroup, width, y, hasLocalDictionaries, regions);

            y += ColumnstoreLayout.GetRowGroupHeight(hasLocalDictionaries) + ColumnstoreLayout.RowGroupGap;
        }

        canvas.Restore();

        return regions;
    }

    private float DrawSeparator(SKCanvas canvas, float width, float y)
    {
        _stroke.Color = MutedColour.WithAlpha(60);

        var lineY = y + ColumnstoreLayout.SeparatorGap + 0.5f;

        canvas.DrawLine(ColumnstoreLayout.Margin, lineY, width - ColumnstoreLayout.Margin, lineY, _stroke);

        return y + (ColumnstoreLayout.SeparatorGap * 2);
    }

    /// <summary>
    /// One band per column running the whole way down, which is what ties a column together across the row groups
    /// </summary>
    /// <remarks>
    /// Drawn in one pass before anything else on the grid rather than per row group, so neither the header nor the
    /// gaps between row groups break it up. Everything after it paints on top, panels covering only the gutter.
    /// </remarks>
    private void DrawColumnBands(SKCanvas canvas,
                                 ColumnStoreIndex index,
                                 IReadOnlyList<RowGroupSummary> rowGroups,
                                 float width,
                                 float top,
                                 float columnHeaderHeight,
                                 bool hasGlobalDictionaries,
                                 bool hasLocalDictionaries)
    {
        if (index.Columns.Count == 0)
        {
            return;
        }

        var bottom = ColumnstoreLayout.BandOverhang
                     + top
                     + columnHeaderHeight
                     + ColumnstoreLayout.SectionLabelBottomPadding
                     + (hasGlobalDictionaries
                            ? ColumnstoreLayout.GlobalDictionaryContainerHeight + ColumnstoreLayout.Margin
                            : 0)
                     + (rowGroups.Count > 0
                            ? (rowGroups.Count
                               * (ColumnstoreLayout.GetRowGroupHeight(hasLocalDictionaries)
                                  + ColumnstoreLayout.RowGroupGap))
                              - ColumnstoreLayout.RowGroupGap
                            : 0);

        if (bottom <= top)
        {
            return;
        }

        var columnWidth = ColumnstoreLayout.GetColumnWidth(width, index.Columns.Count);

        var x = ColumnstoreLayout.GetSegmentsLeft();

        BandTop = top;

        BandBottom = bottom;

        foreach (var column in index.Columns)
        {
            _fill.Color = HoveredColumnId == column.ColumnStoreColumnId
                ? HoverBandColour
                : column.IsLocator
                    ? LocatorBandColour
                    : BandColour;

            canvas.DrawRect(new SKRect(x, top, x + columnWidth, bottom), _fill);

            x += columnWidth + ColumnstoreLayout.SegmentGap;
        }
    }

    /// <summary>
    /// Names the columns once above the row groups, the segments below carrying only what differs between them
    /// </summary>
    private void DrawColumnHeaders(SKCanvas canvas, ColumnStoreIndex index, float width, float y)
    {
        _text.Color = MutedColour;

        canvas.DrawText("Columns",
                        ColumnstoreLayout.Margin + ColumnstoreLayout.ContainerPadding,
                        y + 15,
                        SKTextAlign.Left,
                        _titleFont,
                        _text);

        var columnWidth = ColumnstoreLayout.GetColumnWidth(width, index.Columns.Count);

        var isVertical = UsesVerticalHeaders(index, width);

        var x = ColumnstoreLayout.GetSegmentsLeft();

        foreach (var column in index.Columns)
        {
            _text.Color = TextColour;

            var name = GetHeaderName(column);

            if (isVertical)
            {
                DrawVertical(canvas,
                             name,
                             x + columnWidth,
                             y + ColumnstoreLayout.VerticalColumnHeaderHeight - 4,
                             ColumnstoreLayout.VerticalColumnHeaderHeight - 8,
                             _titleFont);
            }
            else
            {
                DrawClipped(canvas, name, x + 4, y + 14, columnWidth - 8, _titleFont);

                DrawTypeRuns(canvas, column, x + 4, y + 29, columnWidth - 8);
            }

            x += columnWidth + ColumnstoreLayout.SegmentGap;
        }
    }

    // An ordered index is the only thing a column's position in the ordering shows up on
    private static string GetHeaderName(ColumnStoreColumn column)
        => column.IsOrdered ? $"{column.Name} ↑{column.OrderOrdinal}" : column.Name;

    private bool UsesVerticalHeaders(ColumnStoreIndex index, float width)
    {
        var available = ColumnstoreLayout.GetColumnWidth(width, index.Columns.Count) - 8;

        foreach (var column in index.Columns)
        {
            if (_titleFont.MeasureText(GetHeaderName(column)) > available)
            {
                return true;
            }
        }

        return false;
    }

    public float GetColumnHeaderHeight(ColumnStoreIndex index, float width)
        => UsesVerticalHeaders(index, width)
            ? ColumnstoreLayout.VerticalColumnHeaderHeight
            : ColumnstoreLayout.ColumnHeaderHeight;

    /// <summary>
    /// A name turned to read bottom to top, which is the only way one fits a column narrower than it is
    /// </summary>
    private void DrawVertical(SKCanvas canvas, string text, float x, float y, float height, SKFont font)
    {
        canvas.Save();

        canvas.Translate(x, y);

        canvas.RotateDegrees(-90);

        canvas.ClipRect(new SKRect(0, font.Metrics.Ascent, height, font.Metrics.Descent));

        canvas.DrawText(text, 0, 0, SKTextAlign.Left, font, _text);

        canvas.Restore();
    }

    /// <summary>
    /// The column as it was declared, coloured the way the editor colours it
    /// </summary>
    private void DrawTypeRuns(SKCanvas canvas, ColumnStoreColumn column, float x, float y, float available)
    {
        if (column.Structure is not { } structure)
        {
            // A locator has no declared type, so what it holds is written where the type would be
            if (column.IsLocator && column.LocatorDescription.Length > 0)
            {
                _text.Color = MutedColour;

                canvas.DrawText(Fit(column.LocatorDescription, available, _labelFont),
                                x,
                                y,
                                SKTextAlign.Left,
                                _labelFont,
                                _text);
            }

            return;
        }

        var runs = new List<(string Text, SKColor Colour)>
        {
            (SqlDataTypeFormat.GetName(structure.DataType), KeywordColour)
        };

        var arguments = SqlDataTypeFormat.GetArguments(structure.DataType,
                                                       structure.Precision,
                                                       structure.Scale,
                                                       structure.DataLength);

        if (arguments.Count > 0)
        {
            runs.Add(("(", PunctuationColour));

            for (var i = 0; i < arguments.Count; i++)
            {
                if (i > 0)
                {
                    runs.Add((", ", PunctuationColour));
                }

                runs.Add((arguments[i], arguments[i] == "max" ? KeywordColour : NumberColour));
            }

            runs.Add((")", PunctuationColour));
        }

        var total = 0f;

        foreach (var run in runs)
        {
            total += _monoFont.MeasureText(run.Text);
        }

        if (total > available)
        {
            return;
        }

        foreach (var run in runs)
        {
            _text.Color = run.Colour;

            canvas.DrawText(run.Text, x, y, SKTextAlign.Left, _monoFont, _text);

            x += _monoFont.MeasureText(run.Text);
        }
    }

    /// <summary>
    /// Column the pointer is over, which lights its band the whole way down rather than only what is under it
    /// </summary>
    /// <remarks>
    /// Set from where the pointer is rather than from the region under it, so the band lights from anywhere in the
    /// column - the header, the gaps between row groups, and the parts of a row group no box covers.
    /// </remarks>
    public int HoveredColumnId { get; set; } = -1;

    /// <summary>
    /// Where the bands start and stop, which is how far down the pointer still counts as being in a column
    /// </summary>
    public float BandTop { get; private set; }

    public float BandBottom { get; private set; }

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

        _text.Color = TextColour;
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
                DrawDictionaryBox(canvas, bounds, dictionary, column.Name, true, null, regions);
            }
        }

        return container.Bottom + ColumnstoreLayout.Margin;
    }

    /// <summary>
    /// A dictionary as a box of its own, which a global one and a local one are both drawn as
    /// </summary>
    private void DrawDictionaryBox(SKCanvas canvas,
                                   SKRect bounds,
                                   SegmentDictionary dictionary,
                                   string columnName,
                                   bool isGlobal,
                                   SegmentSummary? segment,
                                   List<ColumnstoreRegion> regions)
    {
        var colour = ColumnstoreLayout.GetDictionaryColour(dictionary.Type);

        _fill.Color = colour.WithAlpha(40);
        canvas.DrawRect(bounds, _fill);

        DrawBorder(canvas, bounds, colour);

        // The column is named in the header above and the band it sits in, so the badges take that place
        DrawBadges(canvas, DictionaryBadges(dictionary), bounds.Left + 4, bounds.Top + 4, bounds.Width - 8, 0);

        _text.Color = MutedColour;

        var entryTop = bounds.Top + 4 + BadgeHeight + ColumnstoreLayout.BadgeMargin;

        canvas.DrawText(Fit($"{dictionary.EntryCount} entries", bounds.Width - 8, _labelFont),
                        bounds.Left + 4,
                        entryTop - _labelFont.Metrics.Ascent,
                        SKTextAlign.Left,
                        _labelFont,
                        _text);

        regions.Add(new ColumnstoreRegion
        {
            Bounds = bounds,
            ElementType = ColumnstoreElementType.Dictionary,
            Segment = segment,
            Dictionary = dictionary,
            Label = $"{columnName} {(isGlobal ? "Global" : "Local")} Dictionary",
            Detail = $"{dictionary.EntryCount} entries",
            Details = BuildDictionaryDetails(dictionary, isGlobal, columnName)
        });
    }

    private void DrawRowGroup(SKCanvas canvas,
                              RowGroupSummary rowGroup,
                              float width,
                              float y,
                              bool hasLocalDictionaries,
                              List<ColumnstoreRegion> regions)
    {
        var rowBounds = new SKRect(ColumnstoreLayout.Margin,
                                   y,
                                   width - ColumnstoreLayout.Margin,
                                   y + ColumnstoreLayout.GetRowGroupHeight(hasLocalDictionaries));

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
            var segmentTop = rowBounds.Top + 8
                             + (hasLocalDictionaries ? ColumnstoreLayout.LocalDictionaryRowHeight : 0);

            if (hasLocalDictionaries)
            {
                DrawLocalDictionary(canvas, segment, x, rowBounds.Top, segmentWidth, regions);
            }

            var bounds = new SKRect(x, segmentTop, x + segmentWidth, rowBounds.Bottom - 8);

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

        var metrics = _badgeFont.Metrics;

        var height = metrics.Descent - metrics.Ascent + (ColumnstoreLayout.BadgeVerticalPadding * 2);

        var width = _badgeFont.MeasureText(label) + (ColumnstoreLayout.BadgePadding * 2);

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
                        _badgeFont,
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
                $"{rowGroup.TotalRows} rows, HoBT {rowGroup.DeltaStoreHobtId}");

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
        var colour = ColumnstoreLayout.GetRleTypeColour(segment.RleType);

        _fill.Color = colour.WithAlpha(20);

        canvas.DrawRect(bounds, _fill);

        DrawSizeBar(canvas, segment, bounds, colour, regions);

        DrawBorder(canvas, bounds, colour);

        var contentLeft = bounds.Left
                          + (ColumnstoreLayout.IsNarrow(bounds.Width) ? 0 : ColumnstoreLayout.SizeBarWidth);

        DrawSegmentBadges(canvas, segment, bounds, contentLeft);

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
                new ColumnstoreDetail("Storage", segment.StorageDescription),
                new ColumnstoreDetail("Order", segment.OrderDescription),
                new ColumnstoreDetail("Rows", $"{segment.RowCount}"),
                new ColumnstoreDetail("Data Pointer", segment.DataPointerDescription),
                new ColumnstoreDetail("Derivation", segment.DerivationDescription),
            ]
        });

        DrawSegmentDictionary(canvas, segment, bounds, contentLeft, regions);
    }

    /// <summary>
    /// A bar down the left whose height is the segment's share of the largest segment in the index
    /// </summary>
    private void DrawSizeBar(SKCanvas canvas, SegmentSummary segment, SKRect bounds, SKColor colour, List<ColumnstoreRegion> regions)
    {
        if (ColumnstoreLayout.IsNarrow(bounds.Width))
        {
            return;
        }

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
    private void DrawSegmentBadges(SKCanvas canvas, SegmentSummary segment, SKRect bounds, float contentLeft)
    {
        var left = contentLeft + 4;

        var encoding = new[] { (segment.EncodingDescription, ColumnstoreLayout.GetEncodingColour(segment.Encoding)) };

        DrawBadges(canvas, encoding, left, bounds.Top + BadgeTopMargin, bounds.Right - left - 4, 0);
    }

    /// <summary>
    /// A local dictionary belongs to the one row group, so it sits in a row of its own above the segments
    /// </summary>
    private void DrawLocalDictionary(SKCanvas canvas,
                                     SegmentSummary segment,
                                     float left,
                                     float rowGroupTop,
                                     float width,
                                     List<ColumnstoreRegion> regions)
    {
        if (segment.LocalDictionary is not { } dictionary)
        {
            return;
        }

        var top = rowGroupTop + ColumnstoreLayout.LocalDictionaryGap;

        var bounds = new SKRect(left, top, left + width, top + ColumnstoreLayout.GlobalDictionaryHeight);

        DrawDictionaryBox(canvas, bounds, dictionary, segment.ColumnName, false, segment, regions);
    }

    private void DrawSegmentDictionary(SKCanvas canvas,
                                       SegmentSummary segment,
                                       SKRect segmentBounds,
                                       float contentLeft,
                                       List<ColumnstoreRegion> regions)
    {
        // The box itself is drawn elsewhere, so this is only the marker saying which one the segment reads
        if (segment.Dictionary is not { } dictionary)
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

        var label = isGlobal ? "Global" : "Local";

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
    /// Text cut off at the space it has rather than dropped, the column header always naming its column
    /// </summary>
    /// <remarks>
    /// The rule elsewhere is that a label which will not fit is left out, a shortened value reading as a different
    /// one. A column name is not a value, and the column has nothing else to identify it, so this one is cut.
    /// </remarks>
    private void DrawClipped(SKCanvas canvas, string text, float x, float y, float width, SKFont font)
    {
        var metrics = font.Metrics;

        canvas.Save();

        canvas.ClipRect(new SKRect(x, y + metrics.Ascent, x + width, y + metrics.Descent));

        canvas.DrawText(text, x, y, SKTextAlign.Left, font, _text);

        canvas.Restore();
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
        _monoFont.Dispose();
        _badgeFont.Dispose();
        _roundRect.Dispose();
        _dots.Dispose();
        _titleFont.Dispose();
    }
}
