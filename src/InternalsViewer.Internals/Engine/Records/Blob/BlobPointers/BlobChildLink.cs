using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Address;

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

    [DataStructureItem(ItemType.Rid)]
    public RowIdentifier? RowIdentifier { get; set; }

    [DataStructureItem(ItemType.BlobChildOffset)]
    public int Offset { get; set; }

    [DataStructureItem(ItemType.BlobChildLength)]
    public int Length { get; set; }

    public override string ToString()
    {
        return $"Page: {RowIdentifier} Offset: {Offset} Length: {Length}";
    }
}