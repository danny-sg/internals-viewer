namespace InternalsViewer.Connection.BackupFile.Compression.Index;

/// <summary>
/// Maps the MTF stream of a compressed backup onto the blocks that produce it
/// </summary>
/// <remarks>
/// Built by one pass over the container. The decoded bytes are not kept - only where each block starts and how
/// much of the stream it produces, plus periodic restart points.
/// </remarks>
internal sealed class CompressedBackupIndex(IReadOnlyList<CompressedBlockEntry> blocks,
                                            IReadOnlyList<BackupCheckpoint> checkpoints,
                                            long decompressedLength,
                                            int failedBlockCount)
{
    public IReadOnlyList<CompressedBlockEntry> Blocks { get; } = blocks;

    public IReadOnlyList<BackupCheckpoint> Checkpoints { get; } = checkpoints;

    public long DecompressedLength { get; } = decompressedLength;

    public int FailedBlockCount { get; } = failedBlockCount;

    /// <summary>
    /// Finds the index of the block containing a logical offset
    /// </summary>
    public int FindBlock(long offset)
    {
        var low = 0;

        var high = Blocks.Count - 1;

        while (low <= high)
        {
            var middle = low + (high - low) / 2;

            var block = Blocks[middle];

            if (offset < block.DecompressedOffset)
            {
                high = middle - 1;
            }
            else if (offset >= block.DecompressedEnd)
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
    public BackupCheckpoint? FindCheckpoint(long offset)
    {
        BackupCheckpoint? found = null;

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
