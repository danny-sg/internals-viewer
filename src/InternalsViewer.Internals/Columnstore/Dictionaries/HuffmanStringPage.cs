using System.IO;
using InternalsViewer.Internals.Annotations;
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

    [DataStructureItem(ItemType.HuffmanBlobType)]
    public int HuffmanBlobType { get; set; }

    [DataStructureItem(ItemType.HuffmanBitCount)]
    public int BitCount { get; set; }

    [DataStructureItem(ItemType.HuffmanDecoderBitSize)]
    public int DecoderBitSize { get; set; }

    [DataStructureItem(ItemType.HuffmanCompressedSize)]
    public int CompressedDataSize { get; set; }

    [DataStructureItem(ItemType.HuffmanCharacterSet)]
    public byte CharacterSetCode { get; set; }

    /// <summary>
    /// Four bit code lengths packed two per byte, from which the codes themselves are reconstructed
    /// </summary>
    /// <remarks>
    /// This is the whole of the stored table. Canonical coding assigns codes from the lengths alone, so nothing else
    /// has to be written down.
    /// </remarks>
    public ReadOnlyMemory<byte> CodeLengths { get; set; }

    /// <summary>
    /// Stands in for the packed table in a marker, a hundred and twenty eight bytes of nibbles reading as noise
    /// </summary>
    [DataStructureItem(ItemType.HuffmanCodeLengths)]
    public static string CodeLengthTable => "[Huffman Table]";

    /// <summary>
    /// Bytes between the code lengths and the stream, the stream starting on a four byte boundary
    /// </summary>
    [DataStructureItem(ItemType.StringPageAlignment)]
    public ReadOnlyMemory<byte> Alignment { get; set; }

    public ReadOnlyMemory<byte> Content { get; set; }

    /// <summary>
    /// Stands in for the coded stream in a marker, the bits of it belonging to no one entry
    /// </summary>
    [DataStructureItem(ItemType.StringPagePayload)]
    public string Payload => "[Coded Payload]";

    /// <summary>
    /// The code assigned to each symbol the page uses, available once the table has been built
    /// </summary>
    public IReadOnlyList<HuffmanCode> GetCodes() => _table.GetCodes();

    /// <summary>
    /// What one symbol stands for, a narrow page coding characters where a byte page codes raw bytes
    /// </summary>
    public static string DescribeSymbol(int symbol)
        => symbol is >= 0x20 and < 0x7F ? ((char)symbol).ToString() : string.Empty;

    public override void Mark()
    {
        base.Mark();

        MarkProperty(nameof(HuffmanBlobType), Offset + 0x0C, 4);
        MarkProperty(nameof(BitCount), Offset + 0x10, 4);
        MarkProperty(nameof(DecoderBitSize), Offset + 0x14, 4);
        MarkProperty(nameof(CompressedDataSize), Offset + 0x18, 4);
        MarkProperty(nameof(CharacterSetCode), Offset + 0x1C, 1);
        MarkProperty(nameof(CodeLengthTable), Offset + HeaderSize, CodeLengthTableSize);

        if (DataOffset > HeaderSize + CodeLengthTableSize)
        {
            MarkProperty(nameof(Alignment),
                         Offset + HeaderSize + CodeLengthTableSize,
                         DataOffset - HeaderSize - CodeLengthTableSize);
        }

        if (Size > DataOffset)
        {
            MarkProperty(nameof(Payload), Offset + DataOffset, Size - DataOffset);
        }
    }

    public void Build()
    {
        if (HuffmanBlobType is not (NarrowBlobType or ByteBlobType))
        {
            throw new InvalidDataException($"Huffman string page blob type {HuffmanBlobType} is not supported.");
        }

        _table.Build(CodeLengths.Span);

        _reader.Reset(Content);
    }

    /// <summary>
    /// The symbols read while decoding an entry, with the bits each one came from
    /// </summary>
    /// <remarks>
    /// Materialised rather than streamed because the page decodes through one shared reader and buffer, so the next
    /// read moves the position out from under anything still holding it.
    /// </remarks>
    public IReadOnlyList<HuffmanDecodeStep> Trace(int handleOffset)
    {
        var steps = new List<HuffmanDecodeStep>();

        _reader.SeekBits(handleOffset);

        var first = ReadStep(steps, isLength: true);

        var length = first;

        if ((first & ContinuationFlag) != 0)
        {
            length = DecodeLength(first, ReadStep(steps, isLength: true));
        }

        var symbols = HuffmanBlobType == NarrowBlobType ? length / 2 : length;

        for (var i = 0; i < symbols; i++)
        {
            ReadStep(steps, isLength: false);
        }

        return steps;
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

    private int ReadStep(List<HuffmanDecodeStep> steps, bool isLength)
    {
        var bitOffset = _reader.BitPosition;

        var symbol = ReadSymbol();

        var bitLength = _table.GetCodeLength(symbol);

        steps.Add(new HuffmanDecodeStep(bitOffset, bitLength, symbol, ReadCode(bitOffset, bitLength), isLength));

        return symbol;
    }
    
    private int ReadCode(int bitOffset, int bitLength)
    {
        var position = _reader.BitPosition;

        _reader.SeekBits(bitOffset);

        var code = _reader.Peek(CanonicalHuffmanTable.MaxCodeBits) >> (CanonicalHuffmanTable.MaxCodeBits - bitLength);

        _reader.SeekBits(position);

        return code;
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
