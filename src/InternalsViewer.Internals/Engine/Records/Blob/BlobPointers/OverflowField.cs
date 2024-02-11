using InternalsViewer.Internals.Annotations;

namespace InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;

/// <summary>
/// Row Overflow field
/// </summary>
public class OverflowField : BlobField
{
    [DataStructureItem(ItemType.OverflowLevel)]
    public byte Level { get; set; }

    [DataStructureItem(ItemType.OverflowLength)]
    public int Length { get; set; }

    [DataStructureItem(ItemType.Unused)]
    public byte Unused { get; set; }

    /// <summary>
    /// Update seq (used by optimistic concurrency control for cursors)
    /// </summary>
    [DataStructureItem(ItemType.UpdateSeq)]
    public short UpdateSeq { get; set; }
}