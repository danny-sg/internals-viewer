using System.Buffers.Binary;
using InternalsViewer.Connection.BackupFile.Compression.Chunks;
using InternalsViewer.Connection.BackupFile.Interfaces.Compression;

namespace InternalsViewer.Connection.BackupFile.Compression.Decoders;

/// <summary>
/// Creates the decoder for a compressed backup's payloads
/// </summary>
/// <remarks>
/// Decoders hold reusable buffers and are not shared, so each consumer creates its own.
///
/// The file header declares the algorithm, which is authoritative and costs nothing to read. Sniffing the first chunk is kept as a fallback
/// for a value we do not recognise, because the declared encoding is only known from the two algorithms seen so far.
/// </remarks>
internal static class ChunkDecoderFactory
{
    private const int SniffLength = 256;

    public static IChunkDecoder Create(Stream source)
    {
        return ReadDeclaredAlgorithm(source) switch
        {
            CompressionAlgorithm.Zstd => new ZstdChunkDecoder(),
            CompressionAlgorithm.XpressHuffman => new XpressHuffmanChunkDecoder(),
            _ => SniffFirstChunk(source)
        };
    }

    private static CompressionAlgorithm ReadDeclaredAlgorithm(Stream source)
    {
        var buffer = new byte[sizeof(uint)];

        source.Position = CompressedBackupFormat.AlgorithmOffset;

        source.ReadExactly(buffer);

        return (CompressionAlgorithm)BinaryPrimitives.ReadUInt32LittleEndian(buffer);
    }

    /// <summary>
    /// Identifies the algorithm from the first chunk's payload when the header declares something unrecognised
    /// </summary>
    /// <remarks>
    /// A ZSTD payload is recognisable from its frame magic and an Xpress one from its Huffman code length table, so the payload can settle
    /// what the header could not. A raw first chunk carries neither, and Xpress is the older format, so it is the safer assumption.
    /// </remarks>
    private static IChunkDecoder SniffFirstChunk(Stream source)
    {
        var headerBuffer = new byte[CompressedBackupFormat.ChunkHeaderLength];

        source.Position = CompressedBackupFormat.FileHeaderLength;

        source.ReadExactly(headerBuffer);

        var header = ChunkHeader.Parse(headerBuffer);

        if (header.Type == ChunkType.Compressed)
        {
            var sniffBuffer = new byte[Math.Min(SniffLength, header.PayloadSize)];

            source.Position = CompressedBackupFormat.FileHeaderLength + CompressedBackupFormat.ChunkHeaderLength;

            source.ReadExactly(sniffBuffer);

            var zstd = new ZstdChunkDecoder();

            if (zstd.CanDecode(sniffBuffer))
            {
                return zstd;
            }
        }

        return new XpressHuffmanChunkDecoder();
    }
}
