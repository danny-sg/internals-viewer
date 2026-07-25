using InternalsViewer.Connection.BackupFile.Compression.Chunks;

namespace InternalsViewer.Connection.BackupFile.Compression.Mapping;

/// <summary>
/// Maps the MTF stream of a compressed backup onto the chunk that produced it
/// </summary>
/// <remarks>
/// Built by one pass over the container. The decoded bytes are not kept - only where each chunk starts and how much of the stream it
/// produces, plus periodic restart points.
/// </remarks>
internal sealed class ChunkMap(IReadOnlyList<ChunkEntry> chunks,
                               IReadOnlyList<Checkpoint> checkpoints,
                               long decompressedLength,
                               int failedChunkCount)
{
    public IReadOnlyList<ChunkEntry> Chunks { get; } = chunks;

    public IReadOnlyList<Checkpoint> Checkpoints { get; } = checkpoints;

    public long DecompressedLength { get; } = decompressedLength;

    public int FailedChunkCount { get; } = failedChunkCount;

    /// <summary>
    /// Finds the index of the chunk containing a logical offset
    /// </summary>
    public int FindChunk(long offset)
    {
        var low = 0;

        var high = Chunks.Count - 1;

        while (low <= high)
        {
            var middle = low + (high - low) / 2;

            var chunk = Chunks[middle];

            if (offset < chunk.DecompressedOffset)
            {
                high = middle - 1;
            }
            else if (offset >= chunk.DecompressedEnd)
            {
                low = middle + 1;
            }
            else
            {
                return middle;
            }
        }

        return -1;
    }

    /// <summary>
    /// Finds the latest restart point at or before a logical offset
    /// </summary>
    public Checkpoint? FindCheckpoint(long offset)
    {
        Checkpoint? found = null;

        foreach (var checkpoint in Checkpoints)
        {
            if (checkpoint.DecompressedOffset > offset)
            {
                break;
            }

            found = checkpoint;
        }

        return found;
    }
}
