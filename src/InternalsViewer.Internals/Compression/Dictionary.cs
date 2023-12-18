using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Records;

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