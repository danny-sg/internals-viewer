using System.Text;
using InternalsViewer.Internals.Engine;
using InternalsViewer.Internals.Engine.Annotations;

namespace InternalsViewer.Internals.Compression;

public class Dictionary(int offset) : DataStructure
{
    public int Offset { get; } = offset;

    [DataStructureItem(ItemType.DictionaryEntries)]
    public DictionaryEntry[] DictionaryEntries { get; set; } = Array.Empty<DictionaryEntry>();

    [DataStructureItem(ItemType.DictionaryEntryCount)]
    public int EntryCount { get; set; }

    [DataStructureItem(ItemType.DictionaryColumnOffsets)]
    public ushort[] EntryOffsets { get; set; } = Array.Empty<ushort>();

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Dictionary at {Offset}");
        sb.AppendLine($"Entry count = {EntryCount}");
        sb.AppendLine($"Entry offset = {EntryOffsets}");
        sb.AppendLine();
        sb.AppendLine($"Dictionary entries");
        sb.AppendLine();

        foreach (var entry in DictionaryEntries)
        {
            sb.AppendLine(entry.ToString());
        }

        return sb.ToString();
    }
}