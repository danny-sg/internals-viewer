using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.Compression;

public class DictionaryEntry(byte[] data) : DataStructure
{
    [DataStructureItem(DataStructureItemType.Value)]
    public byte[] Data { get; set; } = data;
}