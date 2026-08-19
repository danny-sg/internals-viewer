using System.IO;

namespace InternalsViewer.Internals.Compression;

/// <summary>
/// MS-XCA Xpress Huffman decoder
/// </summary>
/// <remarks>
/// https://winprotocoldocs-bhdugrdyduf5h2e4.b02.azurefd.net/MS-XCA/%5bMS-XCA%5d.pdf
///
/// Symbol 256 is an ordinary match rather than an end of stream marker, and decoding stops once the declared number of bytes has been
/// produced rather than when the payload runs out, the payload being padded to a word boundary.
///
/// Both compressed backup blocks and archive compressed columnstore segments use this form.
/// </remarks>
public sealed class XpressHuffmanDecoder
{
    public const int TableLength = 256;

    public const int SymbolCount = 512;

    /// <summary>
    /// Offsets are at most fifteen bits plus the implied bit, so 64 KB
    /// </summary>
    public const int MaximumMatchOffset = 65535;

    private readonly CanonicalHuffmanTable _table = new(SymbolCount);

    private readonly HuffmanBitReader _reader = new();

    /// <summary>
    /// Tests whether a payload begins with a complete canonical Huffman code length table
    /// </summary>
    public static bool CanDecode(ReadOnlySpan<byte> payload, int minimumSymbols = 8)
        => payload.Length >= TableLength && CanonicalHuffmanTable.IsComplete(payload[..TableLength], minimumSymbols);

    public void Decode(ReadOnlyMemory<byte> payload, int uncompressedSize, IXpressOutput output)
    {
        if (payload.Length <= TableLength)
        {
            return;
        }

        _table.Build(payload.Span[..TableLength]);

        _reader.Reset(payload, TableLength);

        var start = output.Length;

        while (output.Length - start < uncompressedSize)
        {
            var symbol = _table.Lookup(_reader.Peek(CanonicalHuffmanTable.MaxCodeBits));

            if (symbol == CanonicalHuffmanTable.InvalidSymbol)
            {
                throw new InvalidDataException($"Invalid Huffman code at payload offset {_reader.BytePosition}.");
            }

            _reader.Skip(_table.GetCodeLength(symbol));

            if (symbol < TableLength)
            {
                output.WriteLiteral((byte)symbol);

                continue;
            }

            var match = symbol - TableLength;

            var length = match & 0x0F;

            var offsetBits = match >> 4;

            if (length == 15)
            {
                length = _reader.ReadRawByte();

                if (length == 255)
                {
                    length = _reader.ReadRawUInt16();

                    if (length < 15)
                    {
                        throw new InvalidDataException("Malformed extended match length.");
                    }

                    length -= 15;
                }

                length += 15;
            }

            length += 3;

            output.WriteMatch((1 << offsetBits) + _reader.ReadBits(offsetBits), length);
        }
    }

    /// <summary>
    /// Expands a payload that is known to decompress into a single buffer
    /// </summary>
    public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> payload, int uncompressedSize)
    {
        var output = new XpressBufferOutput(uncompressedSize);

        Decode(payload, uncompressedSize, output);

        return output.Buffer;
    }
}
