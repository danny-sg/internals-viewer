namespace InternalsViewer.Connection.BackupFile.Compression.Chunks;

/// <summary>
/// Marker identifying how a compressed chunk is encoded
/// </summary>
internal enum ChunkType : byte
{
    /// <summary>
    /// Compressed chunk - a code length table followed by a bit stream
    /// </summary>
    Compressed = 0x21,

    /// <summary>
    /// Raw chunk - the payload is stored uncompressed at the next 512 byte boundary
    /// </summary>
    Raw = 0x24,
}
