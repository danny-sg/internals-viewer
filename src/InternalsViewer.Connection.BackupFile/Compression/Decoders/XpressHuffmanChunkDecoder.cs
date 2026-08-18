using InternalsViewer.Connection.BackupFile.Interfaces.Compression;
using InternalsViewer.Internals.Compression;

namespace InternalsViewer.Connection.BackupFile.Compression.Decoders;

/// <summary>
/// Decodes one Xpress Huffman block of a compressed backup into the shared output window
/// </summary>
/// <remarks>
/// https://winprotocoldocs-bhdugrdyduf5h2e4.b02.azurefd.net/MS-XCA/%5bMS-XCA%5d.pdf
/// 
/// This is MS-XCA §2.2 decoding with the framing the backup container uses rather than the framing RtlDecompressBuffer expects, which
/// differs in two ways that matter:
///
///   1. Symbol 256 is an ordinary match (length nibble 0, offset bits 0), NOT an end of stream marker. Treating it as a terminator
///      truncates nearly every block.
///
///   2. A block ends when it has produced the number of bytes its header declares, NOT when the payload runs out - the payload is padded
///      to a word boundary, so input driven termination overshoots or falls short.
///
/// Buffers are reused across blocks - a backup contains thousands of them.
/// </remarks>
internal sealed class XpressHuffmanChunkDecoder : IChunkDecoder
{
    private const int TableLength = 256;

    private const int SymbolCount = 512;

    private const int MinimumTableSymbols = 8;

    private readonly CanonicalHuffmanTable _table = new(SymbolCount);

    private readonly HuffmanBitReader _reader = new();

    /// <summary>
    /// Xpress match offsets are at most 15 bits plus the implied bit, so 64 KB
    /// </summary>
    public int MaximumMatchOffset => 65535;

    /// <summary>
    /// Tests whether a payload begins with a canonical Huffman code length table
    /// </summary>
    /// <remarks>
    /// Code lengths of a canonical Huffman table satisfy Kraft equality - sum(2^-length) == 1 - which arbitrary
    /// bytes effectively never do.
    /// </remarks>
    public bool CanDecode(ReadOnlySpan<byte> payload)
        => payload.Length >= TableLength && CanonicalHuffmanTable.IsComplete(payload[..TableLength], MinimumTableSymbols);

    public void Decode(ReadOnlyMemory<byte> blockPayload, int uncompressedSize, SlidingWindowWriter output)
    {
        if (blockPayload.Length <= TableLength)
        {
            return;
        }

        _table.Build(blockPayload.Span[..TableLength]);

        _reader.Reset(blockPayload, TableLength);

        var produced = 0L;

        var start = output.Length;

        while (produced < uncompressedSize)
        {
            var symbol = _table.Lookup(_reader.Peek(CanonicalHuffmanTable.MaxCodeBits));

            if (symbol == CanonicalHuffmanTable.InvalidSymbol)
            {
                throw new InvalidDataException($"Invalid Huffman code at payload offset {_reader.BytePosition}.");
            }

            _reader.Skip(_table.GetCodeLength(symbol));

            if (symbol < 256)
            {
                output.WriteLiteral((byte)symbol);

                produced = output.Length - start;

                continue;
            }

            var match = symbol - 256;

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

            var offset = (1 << offsetBits) + _reader.ReadBits(offsetBits);

            output.WriteMatch(offset, length);

            produced = output.Length - start;
        }
    }

    public void Dispose()
    {
    }
}
