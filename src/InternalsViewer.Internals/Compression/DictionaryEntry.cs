using InternalsViewer.Internals.Engine;
using InternalsViewer.Internals.Engine.Annotations;

namespace InternalsViewer.Internals.Compression;

public class DictionaryEntry(byte[] data) : DataStructure
{
    [DataStructureItem(DataStructureItemType.Value)]
    public byte[] Data { get; set; } = data;
}