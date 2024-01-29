using System.Collections;
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

    public List<ColumnDescriptor> ColumnDescriptors { get; } = new();

    public short CompressedSize { get; set; }

    public CompressionInfo CompressionInfo { get; set; } = compressionInfo;

    public byte Header { get; set; }

    public short ColumnCountBytes { get; set; }

    public byte[] ShortDataClusterArray { get; set; } = Array.Empty<byte>();

    public byte[] LongDataClusterArray { get; set; } = Array.Empty<byte>();

    public ushort[] ColOffsetArray { get; set; } = Array.Empty<ushort>();
    
    public bool IsCompressedDataRecord { get; set; }
    
    public bool HasVersioning { get; set; }

    public bool HasLongDataRegion { get; set; }
}