using System.Buffers.Binary;
using InternalsViewer.Connection.BackupFile.Compression.Chunks;
using InternalsViewer.Connection.BackupFile.Progress;
using InternalsViewer.Connection.BackupFile.Compression.Decoders;
using InternalsViewer.Connection.BackupFile.Interfaces.Compression;
using Microsoft.Extensions.Logging;
using InternalsViewer.Internals.Engine.Loading;

namespace InternalsViewer.Connection.BackupFile.Compression.Mapping;

/// <summary>
/// Builds the chunk index for a compressed backup without keeping the decompressed stream
/// </summary>
/// <remarks>
/// The container gives no decompressed sizes, so the only way to learn where each chunk lands in the MTF stream is to decode it. This pass
/// does that and throws the bytes away, keeping just the offsets plus a window snapshot every so often so decoding can be restarted near
/// any offset later.
/// </remarks>
internal static class ChunkMapper
{
    private const int TapeSoftFilemarkBlockSizeOffset = 64;

    private const long DefaultCheckpointInterval = 4L * 1024 * 1024;

    /// <summary>
    /// Window size for decoding that is thrown away rather than read back
    /// </summary>
    /// <remarks>
    /// Neither of these passes serves reads, so the window only has to hold the retained history plus the largest single chunk - the
    /// default is sized to double as a read cache, which puts 4 MB on the large object heap for nothing.
    /// </remarks>
    private const int DecodeBufferSize = 256 * 1024;

    public static ChunkMap Build(Stream source,
                                 ILogger logger,
                                 CancellationToken cancellationToken,
                                 IProgress<ProgressDetail>? progress = null,
                                 string label = "Decompressing",
                                 long checkpointInterval = DefaultCheckpointInterval)
    {
        var reporter = new PercentageReporter(progress, label);

        using var decoder = ChunkDecoderFactory.Create(source);

        var writer = new SlidingWindowWriter(Stream.Null, decoder.MaximumMatchOffset, DecodeBufferSize);

        var chunks = new List<ChunkEntry>();

        var checkpoints = new List<Checkpoint>();

        var payloadBuffer = new byte[ushort.MaxValue + 1];

        var failedChunkCount = 0;

        var skippedChunkCount = 0;

        var nextCheckpoint = 0L;

        foreach (var chunk in ChunkWalker.Walk(source, ReadSoftFilemarkBlockSize(source, decoder), decoder))
        {
            cancellationToken.ThrowIfCancellationRequested();

            reporter.Report(chunk.Offset, source.Length);

            if (writer.Length >= nextCheckpoint)
            {
                checkpoints.Add(new Checkpoint(chunks.Count, writer.Length, [.. writer.History]));

                nextCheckpoint = writer.Length + checkpointInterval;
            }

            var start = writer.Length;

            if (payloadBuffer.Length < chunk.PayloadLength)
            {
                payloadBuffer = new byte[chunk.PayloadLength];
            }

            source.Position = chunk.PayloadOffset;

            source.ReadExactly(payloadBuffer, 0, chunk.PayloadLength);

            if (chunk.Header.Type == ChunkType.Compressed)
            {
                if (decoder.CanDecode(payloadBuffer.AsSpan(0, chunk.PayloadLength)))
                {
                    try
                    {
                        decoder.Decode(payloadBuffer.AsMemory(0, chunk.PayloadLength), chunk.UncompressedSize, writer);
                    }
                    catch (InvalidDataException exception)
                    {
                        failedChunkCount++;

                        logger.LogWarning("Chunk at offset {Offset} failed to decode - {Message}",
                                          chunk.Offset,
                                          exception.Message);

                        writer.WriteZeros((int)(chunk.UncompressedSize - (writer.Length - start)));
                    }
                }
                else
                {
                    skippedChunkCount++;

                    writer.WriteZeros(chunk.UncompressedSize);
                }
            }
            else
            {
                writer.WriteRaw(payloadBuffer.AsSpan(0, chunk.PayloadLength));
            }

            chunks.Add(new ChunkEntry(chunk.Offset,
                                      chunk.Header.Type,
                                      chunk.PayloadOffset,
                                      chunk.PayloadLength,
                                      start,
                                      (int)(writer.Length - start)));
        }

        logger.LogDebug("Indexed compressed backup - {ChunkCount} chunks ({FailedChunkCount} failed, " +
                        "{SkippedChunkCount} not Huffman), {Length} bytes",
                        chunks.Count,
                        failedChunkCount,
                        skippedChunkCount,
                        writer.Length);

        return new ChunkMap(chunks, checkpoints, writer.Length, failedChunkCount);
    }

    /// <summary>
    /// Decodes the first chunk to find out how big this backup's soft filemarks are
    /// </summary>
    /// <remarks>
    /// Raw chunks hold a soft filemark and the container gives no length for them. The size comes from the TAPE block, which is what the
    /// first chunk always decodes to, and it varies by producer - locally created backups declare 1 (512 bytes) where the downloaded
    /// SQL 2016 sample declares 8 (4096 bytes).
    ///
    /// Declared in units of <see cref="CompressedBackupFormat.RawChunkAlignment"/>.
    /// </remarks>
    private static int ReadSoftFilemarkBlockSize(Stream source, IChunkDecoder decoder)
    {
        var headerBuffer = new byte[CompressedBackupFormat.ChunkHeaderLength];

        source.Position = CompressedBackupFormat.FileHeaderLength;

        source.ReadExactly(headerBuffer);

        var header = ChunkHeader.Parse(headerBuffer);

        if (header.Type != ChunkType.Compressed)
        {
            return CompressedBackupFormat.RawChunkAlignment;
        }

        var payload = new byte[header.PayloadSize];

        source.Position = CompressedBackupFormat.FileHeaderLength + CompressedBackupFormat.ChunkHeaderLength;

        source.ReadExactly(payload);

        using var tape = new MemoryStream();

        var writer = new SlidingWindowWriter(tape, decoder.MaximumMatchOffset, DecodeBufferSize);

        decoder.Decode(payload, header.UncompressedSize, writer);

        writer.Flush();

        var tapeBlock = tape.ToArray();

        if (tapeBlock.Length < TapeSoftFilemarkBlockSizeOffset + sizeof(ushort))
        {
            return CompressedBackupFormat.RawChunkAlignment;
        }

        var units = BinaryPrimitives.ReadUInt16LittleEndian(tapeBlock.AsSpan(TapeSoftFilemarkBlockSizeOffset));

        return Math.Max((int)units, 1) * CompressedBackupFormat.RawChunkAlignment;
    }
}
