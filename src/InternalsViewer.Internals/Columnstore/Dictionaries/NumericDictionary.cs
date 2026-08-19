using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// Dictionary holding a flat array of numeric values
/// </summary>
public sealed class NumericDictionary : DictionaryBlob
{
    public const int HeaderSize = 56;

    /// <summary>
    /// Opens the hash table header, the fields down to the bucket index mask belonging to it
    /// </summary>
    [DataStructureItem(ItemType.DictionarySubLobType)]
    public SubLobType SubLobType { get; set; }

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

    /// <summary>
    /// Opens the values header, the array of values being a store of its own after the hash table
    /// </summary>
    [DataStructureItem(ItemType.DictionarySubLobType)]
    public SubLobType ValueSubLobType { get; set; }

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
