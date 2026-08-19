using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Metadata.Enums;
using InternalsViewer.UI.App.Models.Columnstore;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Columnstore;

public enum ColumnstoreElementType
{
    RowGroup,
    Segment,
    Dictionary,
    DeleteBitmap,
    DeltaStore
}

/// <summary>
/// A drawn rectangle and what it stands for, so hit testing is a lookup rather than a second layout pass
/// </summary>
public sealed class ColumnstoreRegion
{
    public required SKRect Bounds { get; init; }

    public required ColumnstoreElementType ElementType { get; init; }

    public SegmentSummary? Segment { get; init; }

    public SegmentDictionary? Dictionary { get; init; }

    public RowGroupSummary? RowGroup { get; init; }

    public string Label { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public IReadOnlyList<ColumnstoreDetail> Details { get; init; } = [];
}

/// <summary>
/// Geometry and colours the structure drawing works to
/// </summary>
public static class ColumnstoreLayout
{
    public const float Margin = 12f;

    public const float RowSetBoxHeight = 44f;

    public const float GlobalDictionaryHeight = 34f;

    public const float ContainerHeaderHeight = 20f;

    public const float ContainerPadding = 6f;

    public const float SectionLabelBottomPadding = 6f;

    /// <summary>
    /// The dictionaries container, being its header over one row of blocks
    /// </summary>
    public static float GlobalDictionaryContainerHeight
        => ContainerHeaderHeight + GlobalDictionaryHeight + (ContainerPadding * 2);

    public const float RowGroupHeight = 74f;

    public const float RowGroupGap = 8f;

    public const float MetadataWidth = 150f;

    public const float SegmentGap = 6f;

    /// <summary>
    /// Segments always share the width available, shrinking rather than overflowing a narrow pane
    /// </summary>
    /// <remarks>
    /// The floor only keeps a segment wide enough to click. Labels drop out on their own once they no longer fit.
    /// </remarks>
    public const float SegmentMinWidth = 6f;

    public const float DictionaryHeight = 14f;

    public const float SegmentDictionaryWidth = 80f;

    /// <summary>
    /// Height of the bar along the bottom of a segment whose width shows its share of the largest segment
    /// </summary>
    public const float SizeBarHeight = 8f;

    public const float LabelHeight = 16f;

    /// <summary>
    /// One colour per encoding so the drawing reads as a map of compression techniques
    /// </summary>
    public static SKColor GetEncodingColour(SegmentEncoding encoding) => encoding switch
    {
        SegmentEncoding.ValueBased => new SKColor(0x7F, 0x77, 0xDD),
        SegmentEncoding.ValueHashBased => new SKColor(0x1D, 0x9E, 0x75),
        SegmentEncoding.StringHashBased => new SKColor(0xD8, 0x5A, 0x30),
        SegmentEncoding.StoreByValueBased => new SKColor(0x37, 0x8A, 0xDD),
        SegmentEncoding.StringStoreByValueBased => new SKColor(0xD4, 0x53, 0x7E),
        _ => new SKColor(0x88, 0x87, 0x80)
    };

    public const float BadgePadding = 3f;

    /// <summary>
    /// Space kept above and below the badge, the padding staying tight to the text
    /// </summary>
    public const float BadgeMargin = 2f;

    public const float BadgeCornerRadius = 3f;

    /// <summary>
    /// Background and text for a row group state badge
    /// </summary>
    public static (SKColor Background, SKColor Text) GetStateColours(RowGroupState state) => state switch
    {
        RowGroupState.Invisible => (new SKColor(0xFF, 0xFF, 0xFF), new SKColor(0x5F, 0x5E, 0x5A)),
        RowGroupState.Open => (new SKColor(0xFA, 0xC7, 0x75), new SKColor(0x85, 0x4F, 0x0B)),
        RowGroupState.Closed => (new SKColor(0xB5, 0xD4, 0xF4), new SKColor(0x0C, 0x44, 0x7C)),
        RowGroupState.Compressed => (new SKColor(0xC0, 0xDD, 0x97), new SKColor(0x3B, 0x6D, 0x11)),
        RowGroupState.Tombstone => (new SKColor(0x44, 0x44, 0x41), new SKColor(0xFF, 0xFF, 0xFF)),
        _ => (new SKColor(0xD3, 0xD1, 0xC7), new SKColor(0x2C, 0x2C, 0x2A))
    };

    public static SKColor DictionaryColour => new(0xBA, 0x75, 0x17);

    /// <summary>
    /// Dictionary sub types stay within one colour family, being variants of the same thing
    /// </summary>
    /// <remarks>
    /// Values are syscsdictionaries.type - 1 holds integers, 3 strings and 4 floats.
    /// </remarks>
    public static SKColor GetDictionaryColour(int dictionaryType) => dictionaryType switch
    {
        1 => new SKColor(0x85, 0x4F, 0x0B),
        3 => new SKColor(0xEF, 0x9F, 0x27),
        4 => new SKColor(0xBA, 0x75, 0x17),
        _ => new SKColor(0x88, 0x87, 0x80)
    };

    public static string GetDictionaryTypeDescription(int dictionaryType) => dictionaryType switch
    {
        1 => "Integer",
        3 => "String",
        4 => "Float",
        _ => $"Type {dictionaryType}"
    };

    public static SKColor DeleteBitmapColour => new(0xE2, 0x4B, 0x4A);

    public static SKColor DeltaStoreColour => new(0x63, 0x99, 0x22);

    public static IReadOnlyList<(string Name, SKColor Colour)> Legend =>
    [
        ("Value based", GetEncodingColour(SegmentEncoding.ValueBased)),
        ("Value hash", GetEncodingColour(SegmentEncoding.ValueHashBased)),
        ("String hash", GetEncodingColour(SegmentEncoding.StringHashBased)),
        ("Store by value", GetEncodingColour(SegmentEncoding.StoreByValueBased)),
        ("String store", GetEncodingColour(SegmentEncoding.StringStoreByValueBased)),
        ("Integer dictionary", GetDictionaryColour(1)),
        ("String dictionary", GetDictionaryColour(3)),
        ("Delete bitmap", DeleteBitmapColour),
        ("Delta store", DeltaStoreColour)
    ];

    /// <summary>
    /// Height of the band above the row groups, which only carries the rows sets that are actually present
    /// </summary>
    public static float GetHeaderHeight(bool hasRowSets, int globalDictionaryCount)
        => (hasRowSets ? RowSetBoxHeight + Margin : 0)
           + (globalDictionaryCount > 0 ? GlobalDictionaryContainerHeight + Margin : 0);

    public static float GetContentHeight(int rowGroupCount, float headerHeight)
        => headerHeight
           + (rowGroupCount > 0 ? ContainerHeaderHeight + SectionLabelBottomPadding : 0)
           + (Margin * 2)
           + (rowGroupCount * (RowGroupHeight + RowGroupGap));

    /// <summary>
    /// Segments share the width evenly, the size difference showing as fill rather than as width
    /// </summary>
    public static float GetSegmentWidth(float available, int segmentCount)
    {
        if (segmentCount <= 0)
        {
            return 0;
        }

        var width = (available - ((segmentCount - 1) * SegmentGap)) / segmentCount;

        return Math.Max(width, SegmentMinWidth);
    }
}
