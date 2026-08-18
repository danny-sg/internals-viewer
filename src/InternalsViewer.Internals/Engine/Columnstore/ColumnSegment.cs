using InternalsViewer.Internals.Engine.Columnstore.Enums;

namespace InternalsViewer.Internals.Engine.Columnstore;

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

    public long BaseId { get; set; }

    public double Magnitude { get; set; }

    public long MinDataId { get; set; }

    public long MaxDataId { get; set; }

    public byte[]? MinDeepData { get; set; }

    public byte[]? MaxDeepData { get; set; }

    public int? CollationId { get; set; }

    /// <summary>
    /// Row group local dictionary
    /// </summary>
    public SegmentDictionary? LocalDictionary { get; set; }

    public LobPointer DataPointer { get; set; }

    public Dictionary<string, byte[]>? UnmappedFields { get; set; }

    public int PrimaryDictionaryId { get; set; }

    public int SecondaryDictionaryId { get; set; }

    public int Status { get; set; }

    public short ContainerId { get; set; }

    public long BloomFilterMetadata { get; set; }

    public LobPointer BloomFilterPointer { get; set; }

    public bool HasBloomFilter => !BloomFilterPointer.IsEmpty || BloomFilterMetadata != 0;
}