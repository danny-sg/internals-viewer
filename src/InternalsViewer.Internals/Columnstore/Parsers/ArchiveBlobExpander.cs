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
        var header = ArchiveBlobHeader.Read(data.Span);

        if (header.ExpectedSize != data.Length)
        {
            throw new InvalidDataException($"Archive blob is {data.Length} bytes, header implies {header.ExpectedSize}.");
        }

        var decoder = new XpressHuffmanDecoder();

        return decoder.Decode(data[ArchiveBlobHeader.HeaderSize..], header.UncompressedSize);
    }
}
