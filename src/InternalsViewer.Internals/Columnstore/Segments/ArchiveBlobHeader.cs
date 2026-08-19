using System.Buffers.Binary;

namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// Wrapper an archive compressed segment or dictionary blob carries in place of the structure itself
/// </summary>
/// <remarks>
/// COLUMNSTORE_ARCHIVE applies Xpress Huffman over the whole blob, so the segment header only appears once the payload has been expanded.
/// </remarks>
public readonly record struct ArchiveBlobHeader(int Reserved, int UncompressedSize, int CompressedSize)
{
    public const int HeaderSize = 12;

    public int ExpectedSize => HeaderSize + CompressedSize;

    public static ArchiveBlobHeader Read(ReadOnlySpan<byte> data) => new(BinaryPrimitives.ReadInt32LittleEndian(data[..4]),
                                                                         BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4)),
                                                                         BinaryPrimitives.ReadInt32LittleEndian(data.Slice(8, 4)));

    /// <summary>
    /// Tests whether a blob is archive compressed rather than a structure in its own right
    /// </summary>
    /// <remarks>
    /// A plain blob starts with a version of 1, so a leading zero plus sizes that account for the whole blob separates the two without
    /// having to trust either alone.
    /// </remarks>
    public static bool IsArchive(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
        {
            return false;
        }

        var header = Read(data);

        return header.Reserved == 0
               && header.UncompressedSize > 0
               && header.CompressedSize > 0
               && header.ExpectedSize == data.Length;
    }
}
