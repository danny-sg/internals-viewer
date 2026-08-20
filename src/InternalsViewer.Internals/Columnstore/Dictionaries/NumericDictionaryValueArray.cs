using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

public sealed class NumericDictionaryValueArray : DataStructure
{
    public const int Offset = 0x2C;

    public const int Size = 12;

    [DataStructureItem(ItemType.DictionarySubLobType)]
    public SubLobType SubLobType { get; set; }

    [DataStructureItem(ItemType.DictionaryElementSize)]
    public int ElementSize { get; set; }

    /// <summary>
    /// Values the blob records itself, which the entry count from the metadata is checked against
    /// </summary>
    [DataStructureItem(ItemType.DictionaryValueCount)]
    public int ValueCount { get; set; }

    public void Mark()
    {
        MarkProperty(nameof(SubLobType), Offset, 4);
        MarkProperty(nameof(ElementSize), Offset + 0x04, 4);
        MarkProperty(nameof(ValueCount), Offset + 0x08, 4);
    }
}
