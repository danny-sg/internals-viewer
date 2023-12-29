using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Annotations;
using System.Diagnostics;

namespace InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;

/// <summary>
/// Row Overflow field
/// </summary>
public class OverflowField : BlobField
{
    [DataStructureItem(DataStructureItemType.Level)]
    public byte Level { get; set; }

    [DataStructureItem(DataStructureItemType.OverflowLength)]
    public int Length { get; set; }

    [DataStructureItem(DataStructureItemType.Unused)]
    public byte Unused { get; set; }

    /// <summary>
    /// Update seq (used by optimistic concurrency control for cursors)
    /// </summary>
    [DataStructureItem(DataStructureItemType.UpdateSeq)]
    public short UpdateSeq { get; set; }
}