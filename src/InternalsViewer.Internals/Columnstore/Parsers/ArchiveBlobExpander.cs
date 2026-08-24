using System.IO;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.Internals.Compression;

namespace InternalsViewer.Internals.Columnstore.Parsers;

/// <summary>
/// Expands the Xpress Huffman wrapper an archive compressed blob carries
/// </summary>
public static class ArchiveBlobExpander
{
    public static ReadOnlyMemory<byte> Expand(ReadOnlyMemory<byte> data)
    {
        var blocks = new List<(int Offset, ArchiveBlobHeader Header)>();

        var offset = ArchiveBlobHeader.PrologueSize;

        var total = 0;

        while (offset + ArchiveBlobHeader.HeaderSize <= data.Length)
        {
            var header = ArchiveBlobHeader.Read(data.Span[offset..]);

            if (header is not { UncompressedSize: > 0, CompressedSize: > 0 })
            {
                throw new InvalidDataException($"Archive block at {offset} is {header.CompressedSize} bytes.");
            }

            blocks.Add((offset + ArchiveBlobHeader.HeaderSize, header));

            total += header.UncompressedSize;

            offset += header.BlockSize;
        }

        if (offset != data.Length)
        {
            throw new InvalidDataException($"Archive blob is {data.Length} bytes, blocks end at {offset}.");
        }

        var decoder = new XpressHuffmanDecoder();

        if (blocks.Count == 1)
        {
            return Read(decoder, data, blocks[0].Offset, blocks[0].Header);
        }

        var expanded = new byte[total];

        var written = 0;

        foreach (var (payload, header) in blocks)
        {
            var block = Read(decoder, data, payload, header);

            block.Span.CopyTo(expanded.AsSpan(written));

            written += header.UncompressedSize;
        }

        return expanded;
    }

    /// <summary>
    /// Expands one block, a block that did not compress being held as it is rather than as a payload
    /// </summary>
    /// <remarks>
    /// The sizes matching is what says so, seen on a uniqueidentifier column whose values leave Huffman nothing
    /// to find - four of its five blocks are stored raw.
    /// </remarks>
    private static ReadOnlyMemory<byte> Read(XpressHuffmanDecoder decoder,
                                             ReadOnlyMemory<byte> data,
                                             int offset,
                                             ArchiveBlobHeader header)
        => header.UncompressedSize == header.CompressedSize
            ? data.Slice(offset, header.CompressedSize)
            : decoder.Decode(data.Slice(offset, header.CompressedSize), header.UncompressedSize);
}
