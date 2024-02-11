using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records.FixedVarRecordType;

namespace InternalsViewer.Internals.Engine.Records.Index;

public class IndexRecord : FixedVarRecord
{
    /// <summary>
    /// Down page pointer to the next page in the index
    /// </summary>
    [DataStructureItem(ItemType.DownPagePointer)]
    public PageAddress DownPagePointer { get; set; }

    /// <summary>
    /// RID (Row Identifier) the index is pointing to
    /// </summary>
    [DataStructureItem(ItemType.Rid)]
    public RowIdentifier? Rid { get; set; }

    public bool IncludeKey { get; set; }
    
    public NodeType NodeType { get; set; }
}