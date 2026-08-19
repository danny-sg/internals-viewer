using InternalsViewer.Internals.Annotations;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// Dictionary holding a flat array of numeric values
/// </summary>
public sealed class NumericDictionary : DictionaryBlob
{
    public const int HeaderSize = 56;

    [DataStructureItem(ItemType.DictionaryBucketSize)]
    public int BucketSize { get; set; }

    [DataStructureItem(ItemType.DictionaryBucketCount)]
    public int BucketCount { get; set; }

    [DataStructureItem(ItemType.DictionaryMaxLocalEntryCount)]
    public int MaxLocalEntryCount { get; set; }

    [DataStructureItem(ItemType.DictionaryHashEntrySize)]
    public int HashEntrySize { get; set; }

    [DataStructureItem(ItemType.DictionaryHashEntryCount)]
    public int HashEntryCount { get; set; }

    [DataStructureItem(ItemType.DictionaryCollisionCount)]
    public int CollisionCount { get; set; }

    [DataStructureItem(ItemType.DictionaryBucketIndexMask)]
    public uint BucketIndexMask { get; set; }

    [DataStructureItem(ItemType.DictionaryElementSize)]
    public int ElementSize { get; set; }

    /// <summary>
    /// Values the blob records itself, which the entry count from the metadata is checked against
    /// </summary>
    [DataStructureItem(ItemType.DictionaryValueCount)]
    public int ValueCount { get; set; }

    public long[] Values { get; set; } = [];

    public long GetValue(long dataId) => Values[GetIndex(dataId)];
}
