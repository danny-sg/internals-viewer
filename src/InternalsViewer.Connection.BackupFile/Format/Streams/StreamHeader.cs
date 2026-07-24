using System.Text;
using InternalsViewer.Connection.BackupFile.Reader;
using InternalsViewer.Connection.BackupFile.Format.Blocks.Attributes;

namespace InternalsViewer.Connection.BackupFile.Format.Streams;

internal sealed class StreamHeader
{
    public string StreamId { get; }

    public StreamFileSystemAttributes StreamFileSystemAttributes { get; }

    public StreamMediaFormatAttributes StreamMediaFormatAttributes { get; }

    public ulong StreamLength { get; }

    public ushort DataEncryptionAlgorithm { get; }

    public ushort DataCompressionAlgorithm { get; }

    public ushort Checksum { get; }

    private const int StreamHeaderSize = 22;

    public StreamHeader(BackupReader reader)
    {
        // Check for EOF
        if (reader.BaseStream.Position + StreamHeaderSize >= reader.BaseStream.Length)
        {
            StreamId = string.Empty;

            return;
        }

        StreamId = reader.ReadFixedLengthString(4, StringType.Ansi);

        StreamFileSystemAttributes = (StreamFileSystemAttributes)reader.ReadUInt16();
        StreamMediaFormatAttributes = (StreamMediaFormatAttributes)reader.ReadUInt16();
        
        StreamLength = reader.ReadUInt64();
        
        DataEncryptionAlgorithm = reader.ReadUInt16();
        DataCompressionAlgorithm = reader.ReadUInt16();

        Checksum = reader.ReadUInt16();
    }

    public override string ToString()
    {
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine("Stream Header");
        stringBuilder.AppendLine("=============");
        stringBuilder.AppendLine($"Stream Id:                      {StreamId}");
        stringBuilder.AppendLine($"Stream File System Attributes:  {StreamFileSystemAttributes}");
        stringBuilder.AppendLine($"Stream Media Format Attributes: {StreamMediaFormatAttributes}");
        stringBuilder.AppendLine($"Stream Length:                  {StreamLength}");
        stringBuilder.AppendLine($"Data Encryption Algorithm:      {DataEncryptionAlgorithm}");
        stringBuilder.AppendLine($"Data Compression Algorithm:     {DataCompressionAlgorithm}");
        stringBuilder.AppendLine($"Checksum:                       {Checksum}");
        stringBuilder.AppendLine();

        return stringBuilder.ToString();
    }
}