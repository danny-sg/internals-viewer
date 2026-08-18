namespace InternalsViewer.Internals.Engine.Columnstore;

public sealed class SegmentDictionary
{
    public long HobtId { get; set; }

    public int ColumnId { get; set; }
    
    public int DictionaryId { get; set; }
    
    public long EntryCount { get; set; }
    
    public long OnDiskSize { get; set; }
    
    public LobPointer DataPointer { get; set; }

    public bool IsGlobal => DictionaryId == 0;
}