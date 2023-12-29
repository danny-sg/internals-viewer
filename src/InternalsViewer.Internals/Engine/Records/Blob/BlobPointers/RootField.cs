using InternalsViewer.Internals.Engine.Annotations;

namespace InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;

public class RootField : BlobField
{
    [DataStructureItem(DataStructureItemType.SlotCount)]
    public int SlotCount { get; set; }

    [DataStructureItem(DataStructureItemType.Level)]
    public byte Level { get; set; }

    [DataStructureItem(DataStructureItemType.Unused)]
    public byte Unused { get; set; }

    [DataStructureItem(DataStructureItemType.UpdateSeq)]
    public short UpdateSeq { get; set; }
}