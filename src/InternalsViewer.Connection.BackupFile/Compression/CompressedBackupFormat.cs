namespace InternalsViewer.Connection.BackupFile.Compression;

/// <summary>
/// Constants and detection for the MSSQLBAK compressed backup container
/// </summary>
/// <remarks>
/// A compressed backup is a container holding the whole MTF backup stream compressed.
///
/// A chunk is up to 64 KB of that decompressed stream compressed as one unit, written as a 28 byte header followed by however many bytes
/// it compressed to. The size is defined by what a chunk produces rather than by what it occupies, and the header declares both, so the
/// container can be walked without decoding any of it. Raw chunks are the exception - a fixed 512 bytes stored as they are.
/// </remarks>
internal static class CompressedBackupFormat
{
    public const int FileHeaderLength = 480;

    public const int AlgorithmOffset = 12;

    public const int ChunkHeaderLength = 28;

    public const int RawChunkAlignment = 512;

    public const int RawChunkLength = 512;

    public static ReadOnlySpan<byte> Signature => "MSSQLBAK"u8;

    public static bool IsCompressed(string filename)
    {
        using var stream = File.OpenRead(filename);

        Span<byte> header = stackalloc byte[8];

        return stream.ReadAtLeast(header, header.Length, false) == header.Length
               && header.SequenceEqual(Signature);
    }
}
