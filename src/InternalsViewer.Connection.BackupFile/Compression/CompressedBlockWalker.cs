using System.Buffers.Binary;

namespace InternalsViewer.Connection.BackupFile.Compression;

/// <summary>
/// Walks the block chain of a compressed backup
/// </summary>
/// <remarks>
/// Blocks normally chain - each gives the size of its own payload - but the chain is not continuous. Raw blocks are followed by padding,
/// and there is a trailer section before the final blocks, so where the chain does not land on a valid header the walker scans forward for
/// the next one.
///
/// A candidate is only accepted as a header when its payload parses as a canonical Huffman table (Kraft equality),
/// which payload bytes will not satisfy by chance.
///
/// Raw blocks hold a soft filemark, whose size is declared by the TAPE block of the backup itself, so the caller
/// has to decode that first and pass it in - it is not fixed across producers.
/// </remarks>
internal static class CompressedBlockWalker
{
    public static IEnumerable<CompressedBlockLocation> Walk(Stream source, int rawBlockLength)
    {
        var headerBuffer = new byte[CompressedBackupFormat.BlockHeaderLength];

        var tableBuffer = new byte[CompressedBackupFormat.HuffmanTableLength];

        var position = (long)CompressedBackupFormat.FileHeaderLength;

        while (position + CompressedBackupFormat.BlockHeaderLength <= source.Length)
        {
            if (!IsChainedHeader(source, position, headerBuffer, rawBlockLength))
            {
                var resumed = FindNextHeader(source, position + 1, headerBuffer, tableBuffer, rawBlockLength);

                if (resumed < 0)
                {
                    yield break;
                }

                position = resumed;

                continue;
            }

            source.Position = position;

            source.ReadExactly(headerBuffer);

            var header = CompressedBlockHeader.Parse(headerBuffer);

            if (header.BlockType == CompressedBlockType.Compressed)
            {
                yield return new CompressedBlockLocation(position,
                                                         header,
                                                         position + CompressedBackupFormat.BlockHeaderLength,
                                                         header.PayloadSize,
                                                         header.UncompressedSize);

                position += CompressedBackupFormat.BlockHeaderLength + header.PayloadSize;
            }
            else
            {
                var payloadOffset = Align(position, rawBlockLength);

                yield return new CompressedBlockLocation(position,
                                                         header,
                                                         payloadOffset,
                                                         rawBlockLength,
                                                         rawBlockLength);

                position = payloadOffset + rawBlockLength;
            }
        }
    }

    /// <summary>
    /// Scans for a block to resume from after a section that could not be followed
    /// </summary>
    /// <remarks>
    /// A resume point has to be trustworthy, so this demands a decodable Huffman block, not just a header that
    /// chains - a wrong restart would corrupt everything after it.
    /// </remarks>
    private static long FindNextHeader(Stream source, long from, byte[] headerBuffer, byte[] tableBuffer, int rawBlockLength)
    {
        for (var position = from; position + CompressedBackupFormat.BlockHeaderLength <= source.Length; position++)
        {
            if (!IsChainedHeader(source, position, headerBuffer, rawBlockLength))
            {
                continue;
            }

            if (headerBuffer[0] == (byte)CompressedBlockType.Raw)
            {
                return position;
            }

            source.Position = position + CompressedBackupFormat.BlockHeaderLength;

            source.ReadExactly(tableBuffer);

            if (CompressedBackupFormat.IsCanonicalHuffmanTable(tableBuffer))
            {
                return position;
            }
        }

        return -1;
    }

    /// <summary>
    /// Tests a header by whether the block it describes lands on another block
    /// </summary>
    /// <remarks>
    /// The payload cannot be used to validate here - blocks inside a FILESTREAM section carry payloads that are not Huffman streams, and
    /// rejecting those loses their declared length and shifts the whole stream after.
    /// </remarks>
    private static bool IsChainedHeader(Stream source, long position, byte[] headerBuffer, int rawBlockLength)
    {
        if (position + CompressedBackupFormat.BlockHeaderLength > source.Length)
        {
            return false;
        }

        source.Position = position;

        source.ReadExactly(headerBuffer);

        var marker = (CompressedBlockType)headerBuffer[0];

        if (marker is not (CompressedBlockType.Compressed or CompressedBlockType.Raw))
        {
            return false;
        }

        var next = marker == CompressedBlockType.Raw
            ? Align(position, rawBlockLength) + rawBlockLength
            : position + CompressedBackupFormat.BlockHeaderLength
              + BinaryPrimitives.ReadUInt16LittleEndian(headerBuffer.AsSpan(2));

        if (next + 1 > source.Length)
        {
            return next == source.Length;
        }

        source.Position = next;

        var marker2 = source.ReadByte();

        source.Position = position;

        source.ReadExactly(headerBuffer);

        return marker2 == (byte)CompressedBlockType.Compressed || marker2 == (byte)CompressedBlockType.Raw;
    }

    private static long Align(long value, int alignment) => (value + alignment - 1) / alignment * alignment;
}
