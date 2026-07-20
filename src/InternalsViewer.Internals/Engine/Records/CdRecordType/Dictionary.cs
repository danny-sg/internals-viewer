using System.Text;
using InternalsViewer.Internals.Annotations;

namespace InternalsViewer.Internals.Engine.Records.CdRecordType;

public class Dictionary(int offset) : DataStructure
{
    public int Offset { get; } = offset;

    [DataStructureItem(ItemType.DictionaryEntries)]
    public DictionaryEntry[] DictionaryEntries { get; set; } = [];

    [DataStructureItem(ItemType.DictionaryEntryCount)]
    public int EntryCount { get; set; }

    [DataStructureItem(ItemType.DictionaryColumnOffsets)]
    public ushort[] EntryOffsets { get; set; } = [];

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