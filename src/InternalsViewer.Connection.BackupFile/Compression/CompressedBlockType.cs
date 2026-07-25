namespace InternalsViewer.Connection.BackupFile.Compression;

/// <summary>
/// Block marker identifying how a compressed backup block is encoded
/// </summary>
internal enum CompressedBlockType : byte
{
    /// <summary>
    /// Xpress Huffman compressed block - a code length table followed by a bit stream
    /// </summary>
    Compressed = 0x21,

    /// <summary>
    /// Raw block - the payload is stored uncompressed at the next 512 byte boundary
    /// </summary>
    Raw = 0x24,
}
