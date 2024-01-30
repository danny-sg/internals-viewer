using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Engine.Records.Compressed;

/// <summary>
/// Record in the CD (Column Descriptor) format
/// </summary>
public class CompressedDataRecord(CompressionInfo compressionInfo) : Record
{
    public CompressedRecordType RecordType { get; set; }

    public RowIdentifier RowIdentifier { get; set; }

    public ColumnDescriptor[] ColumnDescriptors { get; set; } = Array.Empty<ColumnDescriptor>();

    public short CompressedSize { get; set; }

    public CompressionInfo CompressionInfo { get; set; } = compressionInfo;

    public byte Header { get; set; }

    public byte[] ShortDataClusterArray { get; set; } = Array.Empty<byte>();

    public byte[] LongDataClusterArray { get; set; } = Array.Empty<byte>();

    public bool IsCompressedDataRecord { get; set; }
    
    public bool HasVersioning { get; set; }

    public bool HasLongDataRegion { get; set; }
    
    public byte LongDataHeader { get; set; }
    
    public ushort LongDataOffsetCount { get; set; }

    public ushort[] LongDataOffsetArray { get; set; } = Array.Empty<ushort>();
}