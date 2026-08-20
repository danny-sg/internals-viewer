using InternalsViewer.Internals.Columnstore.Metadata.Enums;

namespace InternalsViewer.Internals.Columnstore.Metadata;

/// <summary>
/// Columnstore Column Segment
/// </summary>
/// <remarks>
/// Column Segments are the physical storage units for columnstore columns. They contain data for a column within the scope of a rowgroup.
///
/// The interface between a column segment and data consumer is "I want this data for this row". The consumer doesn't know or care about
/// how the column segment manages that, which means different compression and encoding techniques can be used so the compression can be
/// optimized for the data type and data profile.
/// </remarks>
public sealed class ColumnSegment
{
    public SegmentKey Key { get; set; }

    public ColumnStoreColumn? Column { get; set; }

    public int Version { get; set; }

    public SegmentEncoding Encoding { get; set; }

    public int RowCount { get; set; }

    public long OnDiskSize { get; set; }

    public bool HasNulls { get; set; }

    public long? NullValue { get; set; }

    /// <summary>
    /// Anchor value for value prefix/delta encoding
    /// </summary>
    public long BaseId { get; set; }

    /// <summary>
    /// Width/spread of value domain
    /// </summary>
    public double Magnitude { get; set; }

    /// <summary>
    /// Encoded min domain data for segment elimination
    /// </summary>
    public long MinDataId { get; set; }

    /// <summary>
    /// Encoded max domain data for segment elimination
    /// </summary>
    public long MaxDataId { get; set; }

    /// <summary>
    /// Encoded min actual data for segment elimination
    /// </summary>
    public byte[]? MinDeepData { get; set; }

    /// <summary>
    /// Encoded max actual data for segment elimination
    /// </summary>
    public byte[]? MaxDeepData { get; set; }

    public int? CollationId { get; set; }

    /// <summary>
    /// Row group local dictionary
    /// </summary>
    public SegmentDictionary? LocalDictionary { get; set; }

    /// <summary>
    /// Pointer (Row Identifer) to the LOB containing the data
    /// </summary>
    public LobPointer DataPointer { get; set; }

    public Dictionary<string, byte[]>? UnmappedFields { get; set; }
    
    /// <summary>
    /// Global Dictionary Id
    /// </summary>
    public int PrimaryDictionaryId { get; set; }

    /// <summary>
    /// Local Dictionary Id
    /// </summary>
    public int SecondaryDictionaryId { get; set; }

    public int Status { get; set; }

    public short ContainerId { get; set; }

    public long BloomFilterMetadata { get; set; }

    public LobPointer BloomFilterPointer { get; set; }

    public bool HasBloomFilter => !BloomFilterPointer.IsEmpty || BloomFilterMetadata != 0;
}