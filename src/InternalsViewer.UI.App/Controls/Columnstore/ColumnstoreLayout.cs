using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Segments;
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

    public const float ContainerPadding = 8f;

    public const float SectionLabelBottomPadding = 6f;

    /// <summary>
    /// The dictionaries container, being its header over one row of blocks
    /// </summary>
    public static float GlobalDictionaryContainerHeight
        => GlobalDictionaryHeight + (ContainerPadding * 2);

    public const float RowGroupHeight = 82f;

    public const float RowGroupGap = 12f;

    public const float MetadataWidth = 150f;

    public const float SegmentGap = 10f;

    /// <summary>
    /// Where a row of segments begins, the metadata for the row group taking the gutter to its left
    /// </summary>
    public static float GetSegmentsLeft() => Margin + MetadataWidth;

    /// <summary>
    /// Width a row of segments has to share, which the global dictionaries match so the two line up
    /// </summary>
    public static float GetSegmentsAvailable(float width) => width - Margin - GetSegmentsLeft() - SegmentGap;

    /// <summary>
    /// Segments always share the width available, shrinking rather than overflowing a narrow pane
    /// </summary>
    /// <remarks>
    /// The floor only keeps a segment wide enough to click. Labels drop out on their own once they no longer fit.
    /// </remarks>
    public const float SegmentMinWidth = 6f;

    public const float DictionaryHeight = 14f;

    public const float SegmentDictionaryWidth = 60f;

    /// <summary>
    /// Height of the bar along the bottom of a segment whose width shows its share of the largest segment
    /// </summary>
    public const float SizeBarHeight = 8f;

    public const float SizeBarWidth = 8f;

    public const float LabelHeight = 16f;

    /// <summary>
    /// One colour per encoding so the drawing reads as a map of compression techniques
    /// </summary>
    public static SKColor GetEncodingColour(SegmentEncoding encoding) => encoding switch
    {
        SegmentEncoding.ValueBased => ColumnstoreColours.ValueBased,
        SegmentEncoding.ValueHashBased => ColumnstoreColours.ValueHashBased,
        SegmentEncoding.StringHashBased => ColumnstoreColours.StringHashBased,
        SegmentEncoding.StoreByValueBased => ColumnstoreColours.StoreByValueBased,
        SegmentEncoding.StringStoreByValueBased => ColumnstoreColours.StringStoreByValueBased,
        _ => ColumnstoreColours.UnknownEncoding
    };

    public const float BadgePadding = 3f;

    /// <summary>
    /// Padding above and below a badge label, which sits tighter than the padding either side of it
    /// </summary>
    public const float BadgeVerticalPadding = 1f;

    /// <summary>
    /// Space kept above and below the badge, the padding staying tight to the text
    /// </summary>
    public const float BadgeMargin = 2f;

    /// <summary>
    /// Room under a badge, which takes what the vertical padding gave up so the row below is not crowded
    /// </summary>
    public const float BadgeBottomMargin = BadgeMargin + 4f;

    public const float BadgeCornerRadius = 3f;

    /// <summary>
    /// Background and text for a row group state badge
    /// </summary>
    public static (SKColor Background, SKColor Text) GetStateColours(RowGroupState state) => state switch
    {
        RowGroupState.Invisible => (ColumnstoreColours.InvisibleState, ColumnstoreColours.InvisibleStateText),
        RowGroupState.Open => (ColumnstoreColours.OpenState, ColumnstoreColours.OpenStateText),
        RowGroupState.Closed => (ColumnstoreColours.ClosedState, ColumnstoreColours.ClosedStateText),
        RowGroupState.Compressed => (ColumnstoreColours.CompressedState, ColumnstoreColours.CompressedStateText),
        RowGroupState.Tombstone => (ColumnstoreColours.TombstoneState, ColumnstoreColours.TombstoneStateText),
        _ => (ColumnstoreColours.UnknownState, ColumnstoreColours.UnknownStateText)
    };

    /// <summary>
    /// One colour per structure type, a scheme apart from the encodings so the two are not confused
    /// </summary>
    public static SKColor GetStructureColour(SegmentStructureType structureType) => structureType switch
    {
        SegmentStructureType.RunLength => ColumnstoreColours.RunLengthStructure,
        SegmentStructureType.StoreByValue => ColumnstoreColours.StoreByValueStructure,
        _ => ColumnstoreColours.UnknownStructure
    };

    public static SKColor DictionaryColour => ColumnstoreColours.FloatDictionary;

    /// <summary>
    /// Dictionary sub types stay within one colour family, being variants of the same thing
    /// </summary>
    /// <remarks>
    /// Values are syscsdictionaries.type - 1 holds integers, 3 strings and 4 floats.
    /// </remarks>
    public static SKColor GetDictionaryColour(int dictionaryType) => dictionaryType switch
    {
        1 => ColumnstoreColours.NumericDictionary,
        3 => ColumnstoreColours.StringDictionary,
        4 => ColumnstoreColours.FloatDictionary,
        _ => ColumnstoreColours.UnknownDictionary
    };

    public static string GetDictionaryTypeDescription(int dictionaryType) => dictionaryType switch
    {
        1 => "Numeric",
        3 => "String",
        4 => "Float",
        _ => $"Type {dictionaryType}"
    };

    public static SKColor DeleteBitmapColour => ColumnstoreColours.DeleteBitmap;

    public static SKColor DeltaStoreColour => ColumnstoreColours.DeltaStore;

    public static IReadOnlyList<(string Name, SKColor Colour)> Legend =>
    [
        ("Value based", GetEncodingColour(SegmentEncoding.ValueBased)),
        ("Value hash", GetEncodingColour(SegmentEncoding.ValueHashBased)),
        ("String hash", GetEncodingColour(SegmentEncoding.StringHashBased)),
        ("Store by value", GetEncodingColour(SegmentEncoding.StoreByValueBased)),
        ("String store", GetEncodingColour(SegmentEncoding.StringStoreByValueBased)),
        ("Numeric dictionary", GetDictionaryColour(1)),
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
