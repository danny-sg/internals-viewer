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
///   1. Symbol 256 is an ordinary match (length nibble 0, offset bits 0), NOT an end of stream marker. Treating
///      it as a terminator truncates nearly every block.
///
///   2. A block ends when it has produced the number of bytes its header declares, NOT when the payload runs
///      out - the payload is padded to a word boundary, so input driven termination overshoots or falls short.
///
/// Buffers are reused across blocks - a backup contains thousands of them.
/// </remarks>
internal sealed class XpressHuffmanBlockDecoder
{
    private const int SymbolCount = 512;

    private const int MaxCodeBits = 15;

    private const ushort InvalidSymbol = ushort.MaxValue;

    private readonly byte[] _codeLengths = new byte[SymbolCount];

    private readonly ushort[] _decodeTable = new ushort[1 << MaxCodeBits];

    private ReadOnlyMemory<byte> _payload;

    private uint _mask;

    private int _bits;

    private int _position;

    public void Decode(ReadOnlyMemory<byte> blockPayload, int uncompressedSize, SlidingWindowWriter output)
    {
        if (blockPayload.Length <= CompressedBackupFormat.HuffmanTableLength)
        {
            return;
        }

        _payload = blockPayload;

        BuildDecodeTable(blockPayload.Span[..CompressedBackupFormat.HuffmanTableLength]);

        _position = CompressedBackupFormat.HuffmanTableLength;

        _mask = (uint)ReadWord() << 16;
        _mask |= (uint)ReadWord();
        _bits = 16;

        var produced = 0L;

        var start = output.Length;

        while (produced < uncompressedSize)
        {
            var symbol = _decodeTable[_mask >> (32 - MaxCodeBits)];

            if (symbol == InvalidSymbol || _codeLengths[symbol] == 0)
            {
                throw new InvalidDataException($"Invalid Huffman code at payload offset {_position}.");
            }

            Skip(_codeLengths[symbol]);

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
                length = ReadRawByte();

                if (length == 255)
                {
                    length = ReadRawUInt16();

                    if (length < 15)
                    {
                        throw new InvalidDataException("Malformed extended match length.");
                    }

                    length -= 15;
                }

                length += 15;
            }

            length += 3;

            var offset = (1 << offsetBits) + ReadBits(offsetBits);

            output.WriteMatch(offset, length);

            produced = output.Length - start;
        }
    }

    private void BuildDecodeTable(ReadOnlySpan<byte> table)
    {
        for (var i = 0; i < CompressedBackupFormat.HuffmanTableLength; i++)
        {
            _codeLengths[2 * i] = (byte)(table[i] & 0x0F);
            _codeLengths[2 * i + 1] = (byte)(table[i] >> 4);
        }

        _decodeTable.AsSpan().Fill(InvalidSymbol);

        var code = 0;

        for (var bitLength = 1; bitLength <= MaxCodeBits; bitLength++)
        {
            for (var symbol = 0; symbol < SymbolCount; symbol++)
            {
                if (_codeLengths[symbol] != bitLength)
                {
                    continue;
                }

                var start = code << (MaxCodeBits - bitLength);

                var end = start + (1 << (MaxCodeBits - bitLength));

                if (end > _decodeTable.Length)
                {
                    throw new InvalidDataException("Over-subscribed Huffman table.");
                }

                _decodeTable.AsSpan(start, end - start).Fill((ushort)symbol);

                code++;
            }

            code <<= 1;
        }
    }

    private int ReadWord()
    {
        var span = _payload.Span;

        var value = 0;

        if (_position + 1 < span.Length)
        {
            value = span[_position] | (span[_position + 1] << 8);
        }
        else if (_position < span.Length)
        {
            value = span[_position];
        }

        _position += 2;

        return value;
    }

    private byte ReadRawByte()
    {
        var span = _payload.Span;

        var value = _position < span.Length ? span[_position] : (byte)0;

        _position++;

        return value;
    }

    private int ReadRawUInt16() => ReadRawByte() | (ReadRawByte() << 8);

    private void Skip(int count)
    {
        _mask <<= count;
        _bits -= count;

        if (_bits < 0)
        {
            _mask |= (uint)ReadWord() << -_bits;
            _bits += 16;
        }
    }

    private int ReadBits(int count)
    {
        if (count == 0)
        {
            return 0;
        }

        var value = (int)(_mask >> (32 - count));

        Skip(count);

        return value;
    }
}
