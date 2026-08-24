using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Columnstore.Metadata.Enums;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Metadata.Structures;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.Controls.Columnstore;
using SkiaSharp;
using InternalsViewer.UI.App.Models.Columnstore.Segment;

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
    [NotifyPropertyChangedFor(nameof(RleTypeDescription))]
    [NotifyPropertyChangedFor(nameof(RleDescription))]
    [NotifyPropertyChangedFor(nameof(BitPackEntriesDescription))]
    [NotifyPropertyChangedFor(nameof(BitPackSizeDescription))]
    [NotifyPropertyChangedFor(nameof(BookmarkDescription))]
    [NotifyPropertyChangedFor(nameof(Storage))]
    [NotifyPropertyChangedFor(nameof(RleType))]
    [NotifyPropertyChangedFor(nameof(StorageDescription))]
    [NotifyPropertyChangedFor(nameof(DataIdSteps))]
    [NotifyPropertyChangedFor(nameof(ValueSteps))]
    [NotifyPropertyChangedFor(nameof(DerivationDescription))]
    private SegmentBlobHeader? _header;

    public SegmentStorage Storage => SegmentStorageExtensions.Classify(Header);

    public SegmentRleType RleType => Header?.RleType ?? SegmentRleType.Unknown;

    public string StorageDescription => Storage.Describe();

    /// <summary>
    /// How a stored value becomes a data id, being the half of the working the blob answers on its own
    /// </summary>
    public IReadOnlyList<SegmentDerivationStep> DataIdSteps => Split().DataId;

    /// <summary>
    /// How a data id becomes the column's value, which no segment can answer without its metadata
    /// </summary>
    public IReadOnlyList<SegmentDerivationStep> ValueSteps => Split().Value;

    public string DerivationDescription
        => $"Data Id = {string.Join(" ", DataIdSteps.Select(Describe))}"
           + $", Value = {string.Join(" ", ValueSteps.Select(Describe))}";

    private const string Arrow = "→";

    /// <summary>
    /// The working for each result, the result itself being named beside it rather than ending the chain
    /// </summary>
    private (IReadOnlyList<SegmentDerivationStep> DataId, IReadOnlyList<SegmentDerivationStep> Value) Split()
    {
        var steps = BuildSteps();

        var dataId = steps.FindIndex(s => s.Text == "Data Id");

        return (steps[..dataId],
                [steps[dataId] with { Prefix = string.Empty }, .. steps[(dataId + 1)..^1]]);
    }

    private List<SegmentDerivationStep> BuildSteps()
    {
        var steps = new List<SegmentDerivationStep>();

        var minId = Header is null ? "0" : $"{Header.BitpackMinId}";

        switch (Storage)
        {
            case SegmentStorage.VariableLengthData:
                steps.Add(Chip("Stored Value", ColumnstoreColours.VariableLengthDataFlag) with
                {
                    Operator = ">>",
                    Name = "Reserved Bits",
                    Value = "1",
                    Location = "Reserved Low Bit of the Stored Value"
                });
                break;

            case SegmentStorage.BitPack:
                steps.Add(PackedValue(minId));
                break;

            case SegmentStorage.Mixed:
                steps.Add(Chip("RLE Value", ColumnstoreColours.RleFlag));
                steps.Add(PackedValue(minId) with { Prefix = string.Empty });
                break;

            case SegmentStorage.RunLength:
                steps.Add(Chip("RLE Value", ColumnstoreColours.RleFlag));
                break;

            default:
                steps.Add(Chip("Stored Value", ColumnstoreColours.UnknownEncoding));
                break;
        }

        steps[0] = steps[0] with { Prefix = string.Empty };

        steps.Add(new SegmentDerivationStep
        {
            Prefix = Arrow,
            Text = "Data Id",
            Background = SegmentDerivationStep.Black,
            Foreground = SegmentDerivationStep.White,
            Border = SegmentDerivationStep.Black
        });

        steps.AddRange(GetDecodeSteps());

        steps.Add(new SegmentDerivationStep
        {
            Prefix = Arrow,
            Text = "Value",
            Background = SegmentDerivationStep.White,
            Foreground = SegmentDerivationStep.Black,
            Border = SegmentDerivationStep.Outline
        });

        return steps;
    }

    private static string Describe(SegmentDerivationStep step)
    {
        var parts = new List<string>();

        if (step.HasPrefix)
        {
            parts.Add(step.Prefix);
        }

        if (step.HasChip)
        {
            parts.Add(step.Text);
        }

        if (step.HasOperator)
        {
            parts.Add(step.Operator);
        }

        if (step.HasBadge)
        {
            parts.Add($"{step.Name} ({step.Value})");
        }

        if (step.HasSuffix)
        {
            parts.Add(step.Suffix);
        }

        return string.Join(" ", parts);
    }

    private static SegmentDerivationStep Chip(string text, SKColor colour) => new()
    {
        Prefix = Arrow,
        Text = text,
        Background = SegmentDerivationStep.FromSkia(colour),
        Foreground = SegmentDerivationStep.White,
        Border = SegmentDerivationStep.FromSkia(colour)
    };

    private static SegmentDerivationStep PackedValue(string minId)
        => Chip("Packed Value", ColumnstoreColours.BitPackFlag) with
        {
            Operator = "+",
            Name = "Bit Pack Min Id",
            Value = minId,
            Location = "Segment Blob Header, +0x28"
        };

    /// <summary>
    /// What turns the data id into the value, which continues the same expression rather than being a step on
    /// </summary>
    private IEnumerable<SegmentDerivationStep> GetDecodeSteps()
    {
        if (Dictionary is { } dictionary)
        {
            yield return Chip($"{DictionaryDescription} Dictionary", ColumnstoreLayout.GetDictionaryColour(dictionary.Type)) with
            {
                Operator = "[Data Id -",
                Name = "First Id",
                Value = $"{dictionary.LastId - dictionary.EntryCount + 1}",
                Suffix = "]",
                Location = "Metadata.Dictionaries.FirstId",
                BadgeBackground = SegmentDerivationStep.MetadataConstant
            };

            yield break;
        }

        if (Storage == SegmentStorage.VariableLengthData)
        {
            if (Structure is { Scale: > 0 } structure)
            {
                yield return new SegmentDerivationStep
                {
                    Operator = "/ 10^",
                    Name = "Scale",
                    Value = $"{structure.Scale}",
                    Location = "Column.Scale",
                    BadgeBackground = SegmentDerivationStep.MetadataConstant
                };
            }

            yield break;
        }

        if (Segment.BaseId != 0)
        {
            yield return new SegmentDerivationStep
            {
                Operator = "+",
                Name = "Base Id",
                Value = $"{Segment.BaseId}",
                Location = "Metadata.Segments.BaseId",
                BadgeBackground = SegmentDerivationStep.MetadataConstant
            };
        }

        if (Segment.Magnitude > 0 && Math.Abs(Segment.Magnitude - 1) > double.Epsilon)
        {
            yield return new SegmentDerivationStep
            {
                Operator = "x",
                Name = "Magnitude",
                Value = $"{Segment.Magnitude:G}",
                Location = "Metadata.Segments.Magnitude",
                BadgeBackground = SegmentDerivationStep.MetadataConstant
            };
        }
    }

    public string RleTypeDescription
        => Header is null ? string.Empty : Header.RleType.ToString().SplitCamelCase();

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

    public string ColumnName => Segment.Column?.Name ?? $"Column {ColumnId}";

    /// <summary>
    /// Where the column sits in an ordered index's ordering, which no other index has
    /// </summary>
    public string OrderDescription => Segment.Column is { IsOrdered: true } column
        ? $"{column.OrderOrdinal}"
        : string.Empty;

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

    public long BaseId => Segment.BaseId;

    public double Magnitude => Segment.Magnitude;

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
