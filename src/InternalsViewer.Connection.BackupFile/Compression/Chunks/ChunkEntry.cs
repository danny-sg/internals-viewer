namespace InternalsViewer.Connection.BackupFile.Compression.Chunks;

/// <summary>
/// One chunk of a compressed backup, mapping its position in the file to its position in the MTF stream
/// </summary>
internal readonly record struct ChunkEntry(long ChunkOffset,
                                           ChunkType Type,
                                           long PayloadOffset,
                                           int PayloadLength,
                                           long DecompressedOffset,
                                           int DecompressedLength)
{
    public long DecompressedEnd => DecompressedOffset + DecompressedLength;
}
