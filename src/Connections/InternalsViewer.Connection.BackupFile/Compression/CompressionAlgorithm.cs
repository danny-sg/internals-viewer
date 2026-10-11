namespace InternalsViewer.Connection.BackupFile.Compression;

/// <summary>
/// Algorithm a compressed backup declares in its file header
/// </summary>
/// <remarks>
/// Observed values only. The low byte is 3 for both, so the algorithm is carried by the high bits rather than by the whole value, but with
/// two algorithms to go on the encoding is a guess - anything unrecognised falls back to sniffing the first chunk instead.
///
/// Verified level invariant - ZSTD backups taken at LOW, MEDIUM and HIGH all declare 131.
/// </remarks>
internal enum CompressionAlgorithm : uint
{
    XpressHuffman = 3,

    Zstd = 131,
}
