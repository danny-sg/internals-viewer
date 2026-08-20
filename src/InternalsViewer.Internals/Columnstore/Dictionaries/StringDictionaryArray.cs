using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

public sealed class StringDictionaryArray : DataStructure
{
    public const int Size = 12;

    public int Offset { get; set; }

    [DataStructureItem(ItemType.DictionarySubLobType)]
    public SubLobType SubLobType { get; set; }

    [DataStructureItem(ItemType.DictionaryElementSize)]
    public int ElementSize { get; set; }

    [DataStructureItem(ItemType.DictionaryElementCount)]
    public int ElementCount { get; set; }

    public void Mark()
    {
        MarkProperty(nameof(SubLobType), Offset, 4);
        MarkProperty(nameof(ElementSize), Offset + 0x04, 4);
        MarkProperty(nameof(ElementCount), Offset + 0x08, 4);
    }
}
