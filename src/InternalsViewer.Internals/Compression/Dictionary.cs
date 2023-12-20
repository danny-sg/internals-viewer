using InternalsViewer.Internals.Engine;
using InternalsViewer.Internals.Engine.Annotations;
using InternalsViewer.Internals.Engine.Records;

namespace InternalsViewer.Internals.Compression;

public class Dictionary(int offset) : DataStructure
{
    public int Offset { get; set; } = offset;

    public List<DictionaryEntry> DictionaryEntries { get; set; } = new();

    [DataStructureItem(DataStructureItemType.EntryCount)]
    public int EntryCount { get; set; }

    public ushort[] EntryOffset { get; set; } = Array.Empty<ushort>();

    [DataStructureItem(DataStructureItemType.ColumnOffsetArray)]
    public string EntryOffsetArrayDescription => Record.GetArrayString(EntryOffset);
}