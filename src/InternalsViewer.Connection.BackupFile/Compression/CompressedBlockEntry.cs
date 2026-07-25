namespace InternalsViewer.Connection.BackupFile.Compression;

/// <summary>
/// One block of a compressed backup, mapping its position in the file to its position in the MTF stream
/// </summary>
internal readonly record struct CompressedBlockEntry(long BlockOffset,
                                                     CompressedBlockType BlockType,
                                                     long PayloadOffset,
                                                     int PayloadLength,
                                                     long DecompressedOffset,
                                                     int DecompressedLength)
{
    public long DecompressedEnd => DecompressedOffset + DecompressedLength;
}
