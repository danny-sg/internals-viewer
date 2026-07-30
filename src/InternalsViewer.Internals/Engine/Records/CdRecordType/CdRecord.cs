using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Engine.Records.CdRecordType;

/// <summary>
/// Record in the CD (Column Descriptor) format
/// </summary>
public class CdRecord(CompressionInfo compressionInfo) : Record
{
    public CompressedRecordType RecordType { get; set; }

    /// <inheritdoc />
    public override bool IsGhost => RecordType is CompressedRecordType.GhostEmpty
                                              or CompressedRecordType.GhostData
                                              or CompressedRecordType.GhostForwarded
                                              or CompressedRecordType.GhostIndex;

    public RowIdentifier? RowIdentifier { get; set; }

    [DataStructureItem(ItemType.ColumnDescriptors)]
    public ColumnDescriptor[] ColumnDescriptors { get; set; } = [];

    public short CompressedSize { get; set; }

    public CompressionInfo CompressionInfo { get; } = compressionInfo;

    [DataStructureItem(ItemType.Header)]
    public byte Header { get; set; }

    [DataStructureItem(ItemType.ShortDataClusterArray)]
    public byte[] ShortDataClusterArray { get; set; } = [];

    [DataStructureItem(ItemType.LongDataClusterArray)]
    public byte[] LongDataClusterArray { get; set; } = [];

    public bool IsCompressedDataRecord { get; set; }
    
    public bool HasVersioning { get; set; }

    public bool HasLongDataRegion { get; set; }

    [DataStructureItem(ItemType.LongDataHeader)]
    public byte LongDataHeader { get; set; }

    [DataStructureItem(ItemType.LongDataOffsetCount)]
    public ushort LongDataOffsetCount { get; set; }

    [DataStructureItem(ItemType.LongDataOffsetArray)]
    public ushort[] LongDataOffsetArray { get; set; } = [];
}