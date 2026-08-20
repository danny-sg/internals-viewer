using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

public sealed class StringDictionaryStore : DataStructure
{
    public const int Offset = 0x0C;

    public const int Size = 0x2C;

    [DataStructureItem(ItemType.DictionarySubLobType)]
    public SubLobType SubLobType { get; set; }

    /// <summary>
    /// Strings the store holds, which runs one short of the entry count the metadata carries
    /// </summary>
    [DataStructureItem(ItemType.DictionaryStringCount)]
    public int StringCount { get; set; }

    [DataStructureItem(ItemType.DictionaryMaxStringSize)]
    public int MaxStringSize { get; set; }

    [DataStructureItem(ItemType.DictionaryReserved)]
    public byte[] Reserved { get; set; } = [];

    public void Mark()
    {
        MarkProperty(nameof(SubLobType), Offset, 4);
        MarkProperty(nameof(StringCount), Offset + 0x04, 4);
        MarkProperty(nameof(MaxStringSize), Offset + 0x08, 4);
        MarkProperty(nameof(Reserved), Offset + 0x0C, Size - 0x0C);
    }
}
