using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Annotations;

namespace InternalsViewer.Internals.Engine.Records.Index;

public class IndexRecord : Record
{
    public bool IsIndexType(IndexTypes index)
    {
        return (IndexType & index) == index;
    }

    /// <summary>
    /// Gets or sets down page pointer to the next page in the index
    /// </summary>
    [DataStructureItem(DataStructureItemType.DownPagePointer)]
    public PageAddress DownPagePointer { get; set; }

    /// <summary>
    /// Gets or sets the RID (Row Identifier) the index is pointing to
    /// </summary>
    [DataStructureItem(DataStructureItemType.Rid)]
    public RowIdentifier Rid { get; set; }

    public IndexTypes IndexType { get; set; }

    public bool IncludeKey { get; set; }
}