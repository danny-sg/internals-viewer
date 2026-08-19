using System.IO;

namespace InternalsViewer.Internals.Compression;

/// <summary>
/// Canonical Huffman decode table built from packed four-bit code lengths.
/// </summary>
/// <remarks>
/// A Huffman table maps variable-length bit codes to symbols.
///
/// Huffman codes are prefix codes, meaning no code is a prefix of another code. This allows a decoder to read bits until a complete symbol
/// is identified without requiring separators between codes.
///
/// Code lengths are stored packed two per byte. Canonical Huffman coding allows the bit patterns to be reconstructed from the code lengths
/// alone, as both the encoder and decoder apply the same standard code-assignment algorithm. Symbols are assigned codes in order of
/// increasing code length, then by symbol value.
/// </remarks>
public sealed class CanonicalHuffmanTable(int maximumSymbolCount)
{
    public const int MaxCodeBits = 15;

    public const int InvalidSymbol = -1;

    private readonly byte[] _codeLengths = new byte[maximumSymbolCount];

    private readonly ushort[] _decodeTable = new ushort[1 << MaxCodeBits];

    public int SymbolCount { get; private set; }

    public ReadOnlySpan<byte> CodeLengths => _codeLengths.AsSpan(0, SymbolCount);

    /// <summary>
    /// Checks whether the packed code lengths form a complete canonical Huffman code
    /// </summary>
    /// <remarks>
    /// Verifying Kraft equality on the supplied lengths, ensuring they give a complete Huffman code and the entire encoding space is used.
    ///
    /// Each code of length n consumes 2^(MaxCodeBits - n) entries from the decoding space. A complete code uses the entire space exactly
    /// once, so the sum of all contributions must equal 2^MaxCodeBits.
    ///
    /// This is an integer form of the Kraft equality:
    ///
    ///     Σ(2^-length) = 1
    ///
    /// The minimumSymbols parameter prevents degenerate tables with too few active symbols.
    /// </remarks>
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

    /// <summary>
    /// Builds the decode table from a packed array of code lengths.
    /// </summary>
    /// <remarks>
    /// Symbols are ushort values from 0 to SymbolCount - 1. Code lengths are packed two per byte, low nibble first.
    ///
    /// The packed lengths are unpacked into _codeLengths, creating a mapping from symbol to code length.
    ///
    /// Canonical Huffman codes are then reconstructed by processing code lengths from shortest to longest. Symbols with the same code
    /// length are assigned consecutive canonical codes in symbol order.
    ///
    /// Finally, _decodeTable is populated with a fast lookup mapping from Huffman code prefixes to decoded symbols.
    /// 
    /// Example:
    ///
    ///     _codeLengths = [3, 2, 3, 1]
    ///
    ///     Defines:
    ///
    ///         Symbol 0 -> Length 3
    ///         Symbol 1 -> Length 2
    ///         Symbol 2 -> Length 3
    ///         Symbol 3 -> Length 1
    ///
    ///     Canonical code assignment produces:
    ///
    ///         Symbol 3 = 0
    ///         Symbol 1 = 10
    ///         Symbol 0 = 110
    ///         Symbol 2 = 111
    ///
    /// The resulting decode table maps encoded bit patterns back to symbols.
    /// </remarks>
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
                    throw new InvalidDataException("Over-subscribed Huffman table");
                }

                _decodeTable.AsSpan(start, end - start).Fill((ushort)symbol);

                code++;
            }

            code <<= 1;
        }
    }

    public int GetCodeLength(int symbol) => _codeLengths[symbol];

    /// <summary>
    /// Resolves code to symbol
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
