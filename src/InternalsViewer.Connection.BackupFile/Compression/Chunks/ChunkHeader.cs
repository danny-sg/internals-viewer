using System.Buffers.Binary;

namespace InternalsViewer.Connection.BackupFile.Compression.Chunks;

/// <summary>
/// 28 byte header that precedes every chunk in a compressed backup
/// </summary>
/// <remarks>
/// Only the marker, payload size and checksum are meaningful. The bytes between them are not initialized by SQL Server and leak process
/// memory, so they are deliberately not modelled.
/// </remarks>
internal readonly record struct ChunkHeader(ChunkType Type,
                                            byte UncompressedSizeUnits,
                                            int PayloadSize,
                                            uint Checksum)
{
    private const int UncompressedSizeOffset = 1;

    private const int PayloadSizeOffset = 2;

    private const int ChecksumOffset = 24;

    public static ChunkHeader Parse(ReadOnlySpan<byte> data)
    {
        return new ChunkHeader((ChunkType)data[0],
                                data[UncompressedSizeOffset],
                                BinaryPrimitives.ReadUInt16LittleEndian(data[PayloadSizeOffset..]),
                                BinaryPrimitives.ReadUInt32LittleEndian(data[ChecksumOffset..]));
    }

    public bool IsKnownType => Type is ChunkType.Compressed or ChunkType.Raw;

    /// <summary>
    /// Number of bytes this chunk contributes to the MTF stream
    /// </summary>
    /// <remarks>
    /// Held as a count of 512 byte units. Full chunks are 0x80 (64 KB, the Xpress Huffman chunk size) with the first and last chunks of a
    /// backup usually partial.
    ///
    /// This is the only place the decoded size appears - the payload itself is padded to a word boundary, so a decoder that stops when the
    /// input runs out overshoots or falls short.
    /// </remarks>
    public int UncompressedSize => Type == ChunkType.Raw
                                   ? CompressedBackupFormat.RawChunkLength
                                   : UncompressedSizeUnits * CompressedBackupFormat.RawChunkAlignment;
}
