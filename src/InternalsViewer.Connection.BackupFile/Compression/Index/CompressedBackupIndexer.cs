using System.Buffers.Binary;
using InternalsViewer.Connection.BackupFile.Compression.Decoders;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Connection.BackupFile.Compression.Index;

/// <summary>
/// Builds the block index for a compressed backup without keeping the decompressed stream
/// </summary>
/// <remarks>
/// The container gives no decompressed sizes, so the only way to learn where each block lands in the MTF stream is to decode it. This pass
/// does that and throws the bytes away, keeping just the offsets plus a window snapshot every so often so decoding can be restarted near
/// any offset later.
/// </remarks>
internal static class CompressedBackupIndexer
{
    private const int TapeSoftFilemarkBlockSizeOffset = 64;

    private const long DefaultCheckpointInterval = 4L * 1024 * 1024;

    public static CompressedBackupIndex Build(Stream source,
                                              ILogger logger,
                                              CancellationToken cancellationToken,
                                              long checkpointInterval = DefaultCheckpointInterval)
    {
        var writer = new SlidingWindowWriter(Stream.Null);

        var decoder = new XpressHuffmanBlockDecoder();

        var blocks = new List<CompressedBlockEntry>();

        var checkpoints = new List<BackupCheckpoint>();

        var payloadBuffer = new byte[ushort.MaxValue + 1];

        var failedBlockCount = 0;

        var skippedBlockCount = 0;

        var nextCheckpoint = 0L;

        foreach (var block in CompressedBlockWalker.Walk(source, ReadSoftFilemarkBlockSize(source, decoder)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (writer.Length >= nextCheckpoint)
            {
                checkpoints.Add(new BackupCheckpoint(blocks.Count, writer.Length, writer.Window.ToArray()));

                nextCheckpoint = writer.Length + checkpointInterval;
            }

            var start = writer.Length;

            if (payloadBuffer.Length < block.PayloadLength)
            {
                payloadBuffer = new byte[block.PayloadLength];
            }

            source.Position = block.PayloadOffset;

            source.ReadExactly(payloadBuffer, 0, block.PayloadLength);

            if (block.Header.BlockType == CompressedBlockType.Compressed)
            {
                var isHuffman = block.PayloadLength > CompressedBackupFormat.HuffmanTableLength
                                && CompressedBackupFormat.IsCanonicalHuffmanTable(
                                       payloadBuffer.AsSpan(0, CompressedBackupFormat.HuffmanTableLength));

                if (isHuffman)
                {
                    try
                    {
                        decoder.Decode(payloadBuffer.AsMemory(0, block.PayloadLength), block.UncompressedSize, writer);
                    }
                    catch (InvalidDataException exception)
                    {
                        failedBlockCount++;

                        logger.LogWarning("Block at offset {Offset} failed to decode - {Message}",
                                          block.Offset,
                                          exception.Message);

                        writer.WriteZeros((int)(block.UncompressedSize - (writer.Length - start)));
                    }
                }
                else
                {
                    skippedBlockCount++;

                    writer.WriteZeros(block.UncompressedSize);
                }
            }
            else
            {
                writer.WriteRaw(payloadBuffer.AsSpan(0, block.PayloadLength));
            }

            blocks.Add(new CompressedBlockEntry(block.Offset,
                                                block.Header.BlockType,
                                                block.PayloadOffset,
                                                block.PayloadLength,
                                                start,
                                                (int)(writer.Length - start)));
        }

        logger.LogDebug("Indexed compressed backup - {BlockCount} blocks ({FailedBlockCount} failed, " +
                        "{SkippedBlockCount} not Huffman), {Length} bytes",
                        blocks.Count,
                        failedBlockCount,
                        skippedBlockCount,
                        writer.Length);

        return new CompressedBackupIndex(blocks, checkpoints, writer.Length, failedBlockCount);
    }

    /// <summary>
    /// Decodes the first block to find out how big this backup's soft filemarks are
    /// </summary>
    /// <remarks>
    /// Raw blocks hold a soft filemark and the container gives no length for them. The size comes from the TAPE
    /// block, which is what the first block always decodes to, and it varies by producer - locally created
    /// backups declare 1 (512 bytes) where the downloaded SQL 2016 sample declares 8 (4096 bytes).
    ///
    /// Declared in units of <see cref="CompressedBackupFormat.RawBlockAlignment"/>.
    /// </remarks>
    private static int ReadSoftFilemarkBlockSize(Stream source, XpressHuffmanBlockDecoder decoder)
    {
        var headerBuffer = new byte[CompressedBackupFormat.BlockHeaderLength];

        source.Position = CompressedBackupFormat.FileHeaderLength;

        source.ReadExactly(headerBuffer);

        var header = CompressedBlockHeader.Parse(headerBuffer);

        if (header.BlockType != CompressedBlockType.Compressed)
        {
            return CompressedBackupFormat.RawBlockAlignment;
        }

        var payload = new byte[header.PayloadSize];

        source.Position = CompressedBackupFormat.FileHeaderLength + CompressedBackupFormat.BlockHeaderLength;

        source.ReadExactly(payload);

        using var tape = new MemoryStream();

        var writer = new SlidingWindowWriter(tape);

        decoder.Decode(payload, header.UncompressedSize, writer);

        writer.Flush();

        var blocks = tape.ToArray();

        if (blocks.Length < TapeSoftFilemarkBlockSizeOffset + sizeof(ushort))
        {
            return CompressedBackupFormat.RawBlockAlignment;
        }

        var units = BinaryPrimitives.ReadUInt16LittleEndian(blocks.AsSpan(TapeSoftFilemarkBlockSizeOffset));

        return Math.Max((int)units, 1) * CompressedBackupFormat.RawBlockAlignment;
    }
}
