using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Engine.Records.CdRecordType;

/// <summary>
/// Record in the CD (Column Descriptor) format
/// </summary>
public class CompressedDataRecord(CompressionInfo compressionInfo) : Record
{
    public CompressedRecordType RecordType { get; set; }

    public RowIdentifier? RowIdentifier { get; set; }

    [DataStructureItem(ItemType.ColumnDescriptors)]
    public ColumnDescriptor[] ColumnDescriptors { get; set; } = Array.Empty<ColumnDescriptor>();

    public short CompressedSize { get; set; }

    public CompressionInfo CompressionInfo { get; set; } = compressionInfo;

    [DataStructureItem(ItemType.Header)]
    public byte Header { get; set; }

    [DataStructureItem(ItemType.ShortDataClusterArray)]
    public byte[] ShortDataClusterArray { get; set; } = Array.Empty<byte>();

    [DataStructureItem(ItemType.LongDataClusterArray)]
    public byte[] LongDataClusterArray { get; set; } = Array.Empty<byte>();

    public bool IsCompressedDataRecord { get; set; }
    
    public bool HasVersioning { get; set; }

    public bool HasLongDataRegion { get; set; }

    [DataStructureItem(ItemType.LongDataHeader)]
    public byte LongDataHeader { get; set; }

    [DataStructureItem(ItemType.LongDataOffsetCount)]
    public ushort LongDataOffsetCount { get; set; }

    [DataStructureItem(ItemType.LongDataOffsetArray)]
    public ushort[] LongDataOffsetArray { get; set; } = Array.Empty<ushort>();
}