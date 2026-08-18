using System.IO;
using InternalsViewer.Internals.Compression;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// String page whose values are Huffman coded, each independently decodable from its own bit offset
/// </summary>
public sealed class HuffmanStringPage : StringPage
{
    public const int HeaderSize = 29;

    public const int CodeLengthTableSize = 128;

    public const int SymbolCount = CodeLengthTableSize * 2;

    /// <summary>
    /// Compressed stream starts after the code length table, aligned to a four byte boundary
    /// </summary>
    public const int DataOffset = (HeaderSize + CodeLengthTableSize + 3) & ~3;

    private readonly CanonicalHuffmanTable _table = new(SymbolCount);

    private readonly HuffmanBitReader _reader = new();

    private readonly byte[] _buffer = new byte[byte.MaxValue];

    public int HuffmanBlobType { get; set; }

    public int BitCount { get; set; }

    public int DecoderBitSize { get; set; }

    public int CompressedDataSize { get; set; }

    public byte CharacterSetCode { get; set; }

    public ReadOnlyMemory<byte> CodeLengths { get; set; }

    public ReadOnlyMemory<byte> Content { get; set; }

    public void Build()
    {
        _table.Build(CodeLengths.Span);

        _reader.Reset(Content);
    }

    protected override ReadOnlySpan<byte> GetBytes(int handleOffset)
    {
        _reader.SeekBits(handleOffset);

        var length = ReadSymbol();

        for (var i = 0; i < length; i++)
        {
            _buffer[i] = (byte)ReadSymbol();
        }

        return _buffer.AsSpan(0, length);
    }

    private int ReadSymbol()
    {
        var symbol = _table.Lookup(_reader.Peek(CanonicalHuffmanTable.MaxCodeBits));

        if (symbol == CanonicalHuffmanTable.InvalidSymbol)
        {
            throw new InvalidDataException($"Invalid Huffman code at bit position in string page at offset {Offset}.");
        }

        _reader.Skip(_table.GetCodeLength(symbol));

        return symbol;
    }
}
