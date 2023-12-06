using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.BlobPointers;

public class BlobChildLink : Markable
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

    [Mark(MarkType.Rid)]
    public RowIdentifier RowIdentifier { get; set; }

    [Mark(MarkType.BlobChildOffset)]
    public int Offset { get; set; }

    [Mark(MarkType.BlobChildLength)]
    public int Length { get; set; }

    public override string ToString()
    {
        return $"Page: {RowIdentifier} Offset: {Offset} Length: {Length}";
    }
}