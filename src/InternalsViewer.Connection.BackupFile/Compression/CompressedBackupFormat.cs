namespace InternalsViewer.Connection.BackupFile.Compression;

/// <summary>
/// Constants and detection for the MSSQLBAK compressed backup container
/// </summary>
/// <remarks>
/// A compressed backup is not MTF. It is a container holding the whole MTF backup stream compressed with MS_XPRESS (MS-XCA Xpress Huffman)
/// </remarks>
internal static class CompressedBackupFormat
{
    public const int FileHeaderLength = 480;

    public const int BlockHeaderLength = 28;

    public const int RawBlockAlignment = 512;

    public const int RawBlockLength = 512;

    public const int HuffmanTableLength = 256;

    public const int MaximumMatchOffset = 65535;

    public const int MaxCodeBits = 15;

    public static ReadOnlySpan<byte> Signature => "MSSQLBAK"u8;

    /// <summary>
    /// Tests whether a block payload begins with a canonical Huffman code length table
    /// </summary>
    /// <remarks>
    /// Code lengths of a canonical Huffman table satisfy Kraft equality - sum(2^-length) == 1 - which arbitrary bytes effectively never
    /// do.
    ///
    /// Some blocks inside a FILESTREAM section carry a payload that is not a Huffman stream at all, so this is also how a decodable block
    /// is told from one that has to be skipped.
    /// </remarks>
    public static bool IsCanonicalHuffmanTable(ReadOnlySpan<byte> table)
    {
        var total = 0;

        var used = 0;

        foreach (var packed in table)
        {
            var low = packed & 0x0F;

            var high = packed >> 4;

            if (low > 0)
            {
                total += 1 << (MaxCodeBits - low);

                used++;
            }

            if (high > 0)
            {
                total += 1 << (MaxCodeBits - high);

                used++;
            }
        }

        return used >= 8 && total == 1 << MaxCodeBits;
    }

    public static bool IsCompressed(string filename)
    {
        using var stream = File.OpenRead(filename);

        Span<byte> header = stackalloc byte[8];

        return stream.ReadAtLeast(header, header.Length, false) == header.Length
               && header.SequenceEqual(Signature);
    }
}
