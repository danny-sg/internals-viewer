using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.Compression;

public class Dictionary : DataStructure
{
    public Dictionary(byte[] data, int offset)
    {
        Offset = offset;
        LoadDictionary(data, offset);
    }

    private void LoadDictionary(byte[] data, int offset)
    {
        EntryCount = BitConverter.ToInt16(data, offset);

        MarkDataStructure("EntryCount", offset, sizeof(short));

        EntryOffset = new ushort[EntryCount];

        var dataOffset = sizeof(short) + (sizeof(short) * EntryCount);

        MarkDataStructure("EntryOffsetArrayDescription", offset + sizeof(short), EntryCount * sizeof(short));

        for (var i = 0; i < EntryCount; i++)
        {
            EntryOffset[i] = BitConverter.ToUInt16(data, offset + sizeof(short) + (sizeof(short) * i));

            var length = EntryOffset[i] - dataOffset;

            var dictionaryData = new byte[length];

            Array.Copy(data, offset + dataOffset, dictionaryData, 0, length);

            MarkDataStructure("DictionaryEntriesArray", "Dictionary Entry " + i, i);

            var entry = new DictionaryEntry(dictionaryData);

            entry.MarkDataStructure("Data", offset + dataOffset, length);

            DictionaryEntries.Add(entry);

            dataOffset = EntryOffset[i];
        }
    }

    public int Offset { get; set; }

    public List<DictionaryEntry> DictionaryEntries { get; set; } = new();

    [DataStructureItem(DataStructureItemType.EntryCount)]
    public int EntryCount { get; set; }

    public ushort[] EntryOffset { get; set; }

    [DataStructureItem(DataStructureItemType.ColumnOffsetArray)]
    public string EntryOffsetArrayDescription => Record.GetArrayString(EntryOffset);
}