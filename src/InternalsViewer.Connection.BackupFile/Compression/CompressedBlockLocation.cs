namespace InternalsViewer.Connection.BackupFile.Compression;

/// <summary>
/// A located block, with the payload extent resolved
/// </summary>
/// <remarks>
/// A raw block does not carry a usable length - its payload starts at the next 512 byte boundary and runs to the
/// next block, so the walker resolves it and the rest of the code never has to work it out again.
/// </remarks>
internal readonly record struct CompressedBlockLocation(long Offset,
                                                        CompressedBlockHeader Header,
                                                        long PayloadOffset,
                                                        int PayloadLength,
                                                        int UncompressedSize);
