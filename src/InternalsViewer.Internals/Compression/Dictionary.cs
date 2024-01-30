using System.Text;
using InternalsViewer.Internals.Engine;
using InternalsViewer.Internals.Engine.Annotations;
using InternalsViewer.Internals.Engine.Records;

namespace InternalsViewer.Internals.Compression;

public class Dictionary(int offset) : DataStructure
{
    public int Offset { get; set; } = offset;

    public List<DictionaryEntry> DictionaryEntries { get; } = new();

    [DataStructureItem(ItemType.EntryCount)]
    public int EntryCount { get; set; }

    public ushort[] EntryOffset { get; set; } = Array.Empty<ushort>();

    [DataStructureItem(ItemType.ColumnOffsetArray)]
    public string EntryOffsetArrayDescription => RecordHelpers.GetArrayString(EntryOffset);

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Dictionary at {Offset}");
        sb.AppendLine($"Entry count = {EntryCount}");
        sb.AppendLine($"Entry offset = {EntryOffset}");
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