using System.Buffers.Binary;

namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// One Xpress Huffman block of an archive compressed segment or dictionary blob
/// </summary>
/// <remarks>
/// COLUMNSTORE_ARCHIVE compresses the blob in blocks of sixty four kilobytes rather than in one piece, so a blob
/// opens with a reserved dword and then carries a block per sixty four kilobytes of structure, the last holding
/// whatever is left. A blob small enough to fit one block is the same bytes either way, which is why the single
/// block reading held for as long as the lab had nothing larger.
/// </remarks>
public readonly record struct ArchiveBlobHeader(int UncompressedSize, int CompressedSize)
{
    /// <summary>
    /// Reserved dword the blocks follow
    /// </summary>
    public const int PrologueSize = 4;

    public const int HeaderSize = 8;

    /// <summary>
    /// Bytes one block occupies, being its header and the payload behind it
    /// </summary>
    public int BlockSize => HeaderSize + CompressedSize;

    public static ArchiveBlobHeader Read(ReadOnlySpan<byte> data)
        => new(BinaryPrimitives.ReadInt32LittleEndian(data[..4]), BinaryPrimitives.ReadInt32LittleEndian(data.Slice(4, 4)));

    /// <summary>
    /// Tests whether a blob is archive compressed rather than a structure in its own right
    /// </summary>
    /// <remarks>
    /// A plain blob starts with a version of 1, so the leading reserved zero separates the two, and the blocks
    /// having to tile the blob exactly is what stops a structure that happens to start with a zero being taken
    /// for one.
    /// </remarks>
    /// <summary>
    /// Tests a blob whose length is known separately, such as one only its opening bytes have been read from
    /// </summary>
    /// <remarks>
    /// The blocks cannot be walked from a prefix, so the first one only has to fit rather than tile.
    /// </remarks>
    public static bool IsArchive(ReadOnlySpan<byte> data, int blobLength)
    {
        if (data.Length < PrologueSize + HeaderSize || BinaryPrimitives.ReadInt32LittleEndian(data[..4]) != 0)
        {
            return false;
        }

        var header = Read(data[PrologueSize..]);

        return header is { UncompressedSize: > 0, CompressedSize: > 0 }
               && PrologueSize + header.BlockSize <= blobLength;
    }

    public static bool IsArchive(ReadOnlySpan<byte> data)
    {
        if (data.Length < PrologueSize + HeaderSize || BinaryPrimitives.ReadInt32LittleEndian(data[..4]) != 0)
        {
            return false;
        }

        var offset = PrologueSize;

        while (offset + HeaderSize <= data.Length)
        {
            var header = Read(data[offset..]);

            if (header is not { UncompressedSize: > 0, CompressedSize: > 0 })
            {
                return false;
            }

            offset += header.BlockSize;
        }

        return offset == data.Length;
    }
}
