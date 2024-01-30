using InternalsViewer.Internals.Engine.Annotations;

namespace InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;

public class RootField : BlobField
{
    [DataStructureItem(ItemType.SlotCount)]
    public int SlotCount { get; set; }

    [DataStructureItem(ItemType.Level)]
    public byte Level { get; set; }

    [DataStructureItem(ItemType.Unused)]
    public byte Unused { get; set; }

    [DataStructureItem(ItemType.UpdateSeq)]
    public short UpdateSeq { get; set; }
}