using System.IO;

namespace InternalsViewer.Internals.Compression;

/// <summary>
/// Canonical Huffman decode table built from packed four bit code lengths
/// </summary>
public sealed class CanonicalHuffmanTable(int maximumSymbolCount)
{
    public const int MaxCodeBits = 15;

    public const int InvalidSymbol = -1;

    private readonly byte[] _codeLengths = new byte[maximumSymbolCount];

    private readonly ushort[] _decodeTable = new ushort[1 << MaxCodeBits];

    public int SymbolCount { get; private set; }

    public ReadOnlySpan<byte> CodeLengths => _codeLengths.AsSpan(0, SymbolCount);

    /// <summary>
    /// Tests whether the packed code lengths form a complete canonical Huffman code
    /// </summary>
    public static bool IsComplete(ReadOnlySpan<byte> packedCodeLengths, int minimumSymbols = 2)
    {
        var total = 0;

        var used = 0;

        foreach (var packed in packedCodeLengths)
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

        return used >= minimumSymbols && total == 1 << MaxCodeBits;
    }

    public void Build(ReadOnlySpan<byte> packedCodeLengths)
    {
        SymbolCount = packedCodeLengths.Length * 2;

        if (SymbolCount > _codeLengths.Length)
        {
            throw new ArgumentException($"Table holds {SymbolCount} symbols, capacity is {_codeLengths.Length}.",
                                        nameof(packedCodeLengths));
        }

        for (var i = 0; i < packedCodeLengths.Length; i++)
        {
            _codeLengths[2 * i] = (byte)(packedCodeLengths[i] & 0x0F);
            _codeLengths[(2 * i) + 1] = (byte)(packedCodeLengths[i] >> 4);
        }

        _decodeTable.AsSpan().Fill(ushort.MaxValue);

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

    public int GetCodeLength(int symbol) => _codeLengths[symbol];

    /// <summary>
    /// Resolves the symbol a <see cref="MaxCodeBits"/> wide code prefix decodes to
    /// </summary>
    public int Lookup(int codePrefix)
    {
        var symbol = _decodeTable[codePrefix];

        if (symbol == ushort.MaxValue || _codeLengths[symbol] == 0)
        {
            return InvalidSymbol;
        }

        return symbol;
    }
}
