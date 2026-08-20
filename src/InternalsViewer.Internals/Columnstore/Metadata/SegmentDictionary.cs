namespace InternalsViewer.Internals.Columnstore.Metadata;

public sealed class SegmentDictionary
{
    public long HobtId { get; set; }

    public int ColumnId { get; set; }
    
    public int DictionaryId { get; set; }
    
    public int Type { get; set; }

    /// <summary>
    /// Flags the metadata carries, which CSINDEX prints as DictFlag
    /// </summary>
    public long Flags { get; set; }

    public short ContainerId { get; set; }

    /// <summary>
    /// Highest data id the dictionary holds, which with the entry count gives the id of the first entry
    /// </summary>
    public int LastId { get; set; }

    public long EntryCount { get; set; }
    
    public long OnDiskSize { get; set; }
    
    public LobPointer DataPointer { get; set; }

    public bool IsGlobal => DictionaryId == 0;

    /// <summary>
    /// Columns of the metadata row nothing above reads, kept so a field being added is not lost silently
    /// </summary>
    public Dictionary<string, byte[]>? UnmappedFields { get; set; }
}