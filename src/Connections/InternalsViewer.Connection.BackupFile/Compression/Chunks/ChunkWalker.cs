using System.Buffers.Binary;
using InternalsViewer.Connection.BackupFile.Interfaces.Compression;

namespace InternalsViewer.Connection.BackupFile.Compression.Chunks;

/// <summary>
/// Walks the chunk chain of a compressed backup
/// </summary>
/// <remarks>
/// Chunks normally chain - each gives the size of its own payload - but the chain is not continuous. Raw chunks are followed by padding,
/// and there is a trailer section before the final chunks, so where the chain does not land on a valid header the walker scans forward for
/// the next one.
///
/// That scan only trusts a candidate once the decoder recognises its payload. Chaining alone is enough to follow the chain but not to
/// restart it.
///
/// Raw chunks hold a soft filemark, whose size is declared by the TAPE block of the backup itself, so the caller has to decode that first
/// and pass it in - it is not fixed across producers.
/// </remarks>
internal static class ChunkWalker
{
    private const int SniffLength = 256;

    public static IEnumerable<ChunkLocation> Walk(Stream source, int rawChunkLength, IChunkDecoder decoder)
    {
        var headerBuffer = new byte[CompressedBackupFormat.ChunkHeaderLength];

        var sniffBuffer = new byte[SniffLength];

        var position = (long)CompressedBackupFormat.FileHeaderLength;

        while (position + CompressedBackupFormat.ChunkHeaderLength <= source.Length)
        {
            if (!IsChainedHeader(source, position, headerBuffer, rawChunkLength))
            {
                var resumed = FindNextHeader(source, position + 1, headerBuffer, sniffBuffer, rawChunkLength, decoder);

                if (resumed < 0)
                {
                    yield break;
                }

                position = resumed;

                continue;
            }

            source.Position = position;

            source.ReadExactly(headerBuffer);

            var header = ChunkHeader.Parse(headerBuffer);

            if (header.Type == ChunkType.Compressed)
            {
                yield return new ChunkLocation(position,
                                               header,
                                               position + CompressedBackupFormat.ChunkHeaderLength,
                                               header.PayloadSize,
                                               header.UncompressedSize);

                position += CompressedBackupFormat.ChunkHeaderLength + header.PayloadSize;
            }
            else
            {
                var payloadOffset = Align(position, rawChunkLength);

                yield return new ChunkLocation(position,
                                               header,
                                               payloadOffset,
                                               rawChunkLength,
                                               rawChunkLength);

                position = payloadOffset + rawChunkLength;
            }
        }
    }

    /// <summary>
    /// Scans for a chunk to resume from after a section that could not be followed
    /// </summary>
    /// <remarks>
    /// A resume point has to be trustworthy, so this demands a chunk the decoder recognises, not just a header that chains - a wrong
    /// restart would corrupt everything after it.
    ///
    /// A raw marker is taken on chaining alone, having no compressed payload to recognise.
    /// </remarks>
    private static long FindNextHeader(Stream source,
                                       long from,
                                       byte[] headerBuffer,
                                       byte[] sniffBuffer,
                                       int rawChunkLength,
                                       IChunkDecoder decoder)
    {
        for (var position = from; position + CompressedBackupFormat.ChunkHeaderLength <= source.Length; position++)
        {
            if (!IsChainedHeader(source, position, headerBuffer, rawChunkLength))
            {
                continue;
            }

            if (headerBuffer[0] == (byte)ChunkType.Raw)
            {
                return position;
            }

            source.Position = position + CompressedBackupFormat.ChunkHeaderLength;

            var sniffed = source.Read(sniffBuffer, 0, sniffBuffer.Length);

            if (decoder.CanDecode(sniffBuffer.AsSpan(0, sniffed)))
            {
                return position;
            }
        }

        return -1;
    }

    /// <summary>
    /// Tests a header by whether the chunk it describes lands on another chunk
    /// </summary>
    /// <remarks>
    /// The payload cannot be used to validate here - chunks inside a FILESTREAM section carry payloads that are not decodable streams, and
    /// rejecting those loses their declared length and shifts the whole stream after.
    /// </remarks>
    private static bool IsChainedHeader(Stream source, long position, byte[] headerBuffer, int rawChunkLength)
    {
        if (position + CompressedBackupFormat.ChunkHeaderLength > source.Length)
        {
            return false;
        }

        source.Position = position;

        source.ReadExactly(headerBuffer);

        var marker = (ChunkType)headerBuffer[0];

        if (marker is not (ChunkType.Compressed or ChunkType.Raw))
        {
            return false;
        }

        var next = marker == ChunkType.Raw
            ? Align(position, rawChunkLength) + rawChunkLength
            : position + CompressedBackupFormat.ChunkHeaderLength
              + BinaryPrimitives.ReadUInt16LittleEndian(headerBuffer.AsSpan(2));

        if (next + 1 > source.Length)
        {
            return next == source.Length;
        }

        source.Position = next;

        var marker2 = source.ReadByte();

        source.Position = position;

        source.ReadExactly(headerBuffer);

        return marker2 == (byte)ChunkType.Compressed || marker2 == (byte)ChunkType.Raw;
    }

    private static long Align(long value, int alignment) => (value + alignment - 1) / alignment * alignment;
}
