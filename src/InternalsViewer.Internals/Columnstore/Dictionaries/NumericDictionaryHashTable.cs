using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

public sealed class NumericDictionaryHashTable : DataStructure
{
    public const int Offset = 0x0C;

    public const int Size = 32;

    [DataStructureItem(ItemType.DictionarySubLobType)]
    public SubLobType SubLobType { get; set; }

    [DataStructureItem(ItemType.DictionaryBucketSize)]
    public int BucketSize { get; set; }

    [DataStructureItem(ItemType.DictionaryBucketCount)]
    public int BucketCount { get; set; }

    [DataStructureItem(ItemType.DictionaryMaxLocalEntryCount)]
    public int MaxLocalEntryCount { get; set; }

    [DataStructureItem(ItemType.DictionaryHashEntrySize)]
    public int EntrySize { get; set; }

    [DataStructureItem(ItemType.DictionaryHashEntryCount)]
    public int EntryCount { get; set; }

    [DataStructureItem(ItemType.DictionaryCollisionCount)]
    public int CollisionCount { get; set; }

    [DataStructureItem(ItemType.DictionaryBucketIndexMask)]
    public uint BucketIndexMask { get; set; }

    public bool IsPopulated => BucketCount > 0 || EntryCount > 0;

    public void Mark()
    {
        MarkProperty(nameof(SubLobType), Offset, 4);
        MarkProperty(nameof(BucketSize), Offset + 0x04, 4);
        MarkProperty(nameof(BucketCount), Offset + 0x08, 4);
        MarkProperty(nameof(MaxLocalEntryCount), Offset + 0x0C, 4);
        MarkProperty(nameof(EntrySize), Offset + 0x10, 4);
        MarkProperty(nameof(EntryCount), Offset + 0x14, 4);
        MarkProperty(nameof(CollisionCount), Offset + 0x18, 4);
        MarkProperty(nameof(BucketIndexMask), Offset + 0x1C, 4);
    }
}
