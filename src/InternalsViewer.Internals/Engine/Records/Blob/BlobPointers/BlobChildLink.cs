using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Annotations;

namespace InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;

public class BlobChildLink : DataStructure
{
    public BlobChildLink()
    {
    }

    public BlobChildLink(RowIdentifier rowIdentifier, int offset, int length)
    {
        RowIdentifier = rowIdentifier;
        Offset = offset;
        Length = length;
    }

    [DataStructureItem(DataStructureItemType.Rid)]
    public RowIdentifier RowIdentifier { get; set; }

    [DataStructureItem(DataStructureItemType.BlobChildOffset)]
    public int Offset { get; set; }

    [DataStructureItem(DataStructureItemType.BlobChildLength)]
    public int Length { get; set; }

    public override string ToString()
    {
        return $"Page: {RowIdentifier} Offset: {Offset} Length: {Length}";
    }
}