using System.IO;
using InternalsViewer.Internals.Compression;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// String page whose values are Huffman coded, each independently decodable from its own bit offset
/// </summary>
public sealed class HuffmanStringPage : StringPage
{
    /// <summary>
    /// Each symbol is one character, the entry length still counting bytes
    /// </summary>
    /// <remarks>
    /// Chosen for a wide column whose characters all fit in a byte, halving the symbols the stream has to carry.
    /// </remarks>
    public const int NarrowBlobType = 1;

    /// <summary>
    /// Each symbol is one byte of the value
    /// </summary>
    public const int ByteBlobType = 2;

    public const int MaximumStringSize = 8192;

    public const int HeaderSize = 29;

    public const int CodeLengthTableSize = 128;

    public const int SymbolCount = CodeLengthTableSize * 2;

    /// <summary>
    /// Compressed stream starts after the code length table, aligned to a four byte boundary
    /// </summary>
    public const int DataOffset = (HeaderSize + CodeLengthTableSize + 3) & ~3;

    private readonly CanonicalHuffmanTable _table = new(SymbolCount);

    private readonly HuffmanBitReader _reader = new();

    private readonly byte[] _buffer = new byte[MaximumStringSize];

    public int HuffmanBlobType { get; set; }

    public int BitCount { get; set; }

    public int DecoderBitSize { get; set; }

    public int CompressedDataSize { get; set; }

    public byte CharacterSetCode { get; set; }

    public ReadOnlyMemory<byte> CodeLengths { get; set; }

    public ReadOnlyMemory<byte> Content { get; set; }

    public void Build()
    {
        if (HuffmanBlobType is not (NarrowBlobType or ByteBlobType))
        {
            throw new InvalidDataException($"Huffman string page blob type {HuffmanBlobType} is not supported.");
        }

        _table.Build(CodeLengths.Span);

        _reader.Reset(Content);
    }

    protected override ReadOnlySpan<byte> GetBytes(int handleOffset)
    {
        _reader.SeekBits(handleOffset);

        var length = ReadLength();

        if (HuffmanBlobType == NarrowBlobType)
        {
            if ((length & 1) != 0)
            {
                throw new InvalidDataException($"Narrow Huffman entry length {length} is not a whole number of characters.");
            }

            for (var i = 0; i < length; i += 2)
            {
                _buffer[i] = (byte)ReadSymbol();
                _buffer[i + 1] = 0;
            }
        }
        else
        {
            for (var i = 0; i < length; i++)
            {
                _buffer[i] = (byte)ReadSymbol();
            }
        }

        return _buffer.AsSpan(0, length);
    }

    private int ReadLength()
    {
        var first = ReadSymbol();

        return (first & ContinuationFlag) == 0 ? first : DecodeLength(first, ReadSymbol());
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
