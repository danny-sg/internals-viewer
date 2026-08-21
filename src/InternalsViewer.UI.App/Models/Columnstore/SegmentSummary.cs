using System;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Columnstore.Metadata.Enums;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Metadata.Structures;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// One column segment as the viewer presents it
/// </summary>
public sealed partial class SegmentSummary : ObservableObject
{
    public required ColumnSegment Segment { get; init; }

    /// <summary>
    /// The segment blob's prologue, read separately from the metadata and applied once it arrives
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StructureDescription))]
    [NotifyPropertyChangedFor(nameof(RleDescription))]
    [NotifyPropertyChangedFor(nameof(BitPackEntriesDescription))]
    [NotifyPropertyChangedFor(nameof(BitPackSizeDescription))]
    [NotifyPropertyChangedFor(nameof(BookmarkDescription))]
    private SegmentBlobHeader? _header;

    public string StructureDescription
        => Header is null ? string.Empty : Header.StructureType.ToString().SplitCamelCase();

    public string RleDescription => Header is null
        ? string.Empty
        : Header.HasRleArray
            ? $"{Header.RleEntryCount}"
            : "None";

    public string BitPackEntriesDescription => Header is null
        ? string.Empty
        : Header.HasBitpackArray
            ? $"{Header.BitpackValueCount}"
            : "None";

    public string BitPackSizeDescription => Header is null
        ? string.Empty
        : Header.HasBitpackArray
            ? $"{Header.BitpackEntrySize} bits"
            : "None";

    public string BookmarkDescription => Header is null
        ? string.Empty
        : Header.BookmarkCount == 0
            ? "None"
            : $"{Header.BookmarkCount} every {Header.BookmarkDistance}";

    public required long LargestSegmentSize { get; init; }

    public int ColumnId => Segment.Key.ColumnId;

    public int RowGroupId => Segment.Key.RowGroupId;

    public string SegmentDescription => $"Row Group {RowGroupId} Segment {Segment.ContainerId}";

    public string ColumnName => Segment.Column?.Name ?? $"Column {ColumnId}";

    /// <summary>
    /// The column the segment holds, which carries the type the values were declared as
    /// </summary>
    public ColumnStructure? Structure => Segment.Column?.Structure;

    public SegmentEncoding Encoding => Segment.Encoding;

    public string EncodingDescription => Encoding.ToString().SplitCamelCase().Replace(" Based", string.Empty);

    public long OnDiskSize => Segment.OnDiskSize;

    public int RowCount => Segment.RowCount;

    public bool HasNulls => Segment.HasNulls;

    public SegmentDictionary? LocalDictionary => Segment.LocalDictionary;

    public SegmentDictionary? GlobalDictionary => Segment.Column?.GlobalDictionary;

    public bool HasLocalDictionary => LocalDictionary is not null;

    public bool HasGlobalDictionary => GlobalDictionary is not null;

    /// <summary>
    /// Which dictionary the segment reads, a local one taking precedence over the column's global one
    /// </summary>
    public SegmentDictionary? Dictionary => LocalDictionary ?? GlobalDictionary;

    public bool HasDictionary => Dictionary is not null;

    public string DictionaryScope => Dictionary is null
        ? string.Empty
        : HasLocalDictionary
            ? "Local"
            : "Global";

    public string DictionaryDescription => HasLocalDictionary
        ? $"Local {LocalDictionary!.DictionaryId}"
        : HasGlobalDictionary
            ? $"Global {GlobalDictionary!.DictionaryId}"
            : string.Empty;

    public long MinDataId => Segment.MinDataId;

    public long MaxDataId => Segment.MaxDataId;

    /// <summary>
    /// Actual lowest and highest values, which only the dictionary encoded string columns record
    /// </summary>
    public object? MinValue => ColumnstoreValueConverter.ConvertDeepData(Segment.MinDeepData, Segment.Column?.Structure);

    public object? MaxValue => ColumnstoreValueConverter.ConvertDeepData(Segment.MaxDeepData, Segment.Column?.Structure);

    public string MinValueDescription => Describe(MinValue);

    public string MaxValueDescription => Describe(MaxValue);

    private static string Describe(object? value) => value switch
    {
        null => string.Empty,
        byte[] bytes => Convert.ToHexString(bytes),
        _ => value.ToString() ?? string.Empty
    };

    public LobPointer DataPointer => Segment.DataPointer;

    public PageAddress DataPage => DataPointer.PageAddress;

    public ushort DataSlot => (ushort)DataPointer.Slot;

    public bool HasDataPointer => !DataPointer.IsEmpty;

    public string DataPointerDescription => HasDataPointer ? $"({DataPage.FileId}:{DataPage.PageId}:{DataSlot})" : string.Empty;

    /// <summary>
    /// Size relative to the largest segment in the index, which the drawing uses for the size bar width
    /// </summary>
    public double SizeFraction => LargestSegmentSize > 0
        ? Math.Clamp((double)OnDiskSize / LargestSegmentSize, 0, 1)
        : 0;

    /// <summary>
    /// Bytes each row costs, which is the useful comparison across columns of different widths
    /// </summary>
    public double BytesPerRow => RowCount > 0 ? (double)OnDiskSize / RowCount : 0;
}
