using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Annotations;

namespace InternalsViewer.Internals.Engine.Records.Index;

public class IndexRecord : Record
{
    /// <summary>
    /// Down page pointer to the next page in the index
    /// </summary>
    [DataStructureItem(DataStructureItemType.DownPagePointer)]
    public PageAddress DownPagePointer { get; set; }

    /// <summary>
    /// RID (Row Identifier) the index is pointing to
    /// </summary>
    [DataStructureItem(DataStructureItemType.Rid)]
    public RowIdentifier Rid { get; set; }

    public bool IncludeKey { get; set; }
    
    public NodeType NodeType { get; set; }
}