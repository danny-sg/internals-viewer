using System.Buffers.Binary;
using System.IO;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Compression;

namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// Page of a store by value segment, holding a fixed width value array under Xpress Huffman
/// </summary>
public sealed class SegmentValuePage : DataStructure
{
    /// <summary>
    /// Header of a compressed page, the trailing size only being there because there is a payload to size
    /// </summary>
    public const int HeaderSize = 14;

    /// <summary>
    /// Header of a page held raw, whose values start straight after it
    /// </summary>
    public const int RawHeaderSize = 12;

    /// <summary>
    /// Value the compression nibble takes when the payload is Xpress Huffman rather than raw
    /// </summary>
    public const int HuffmanCompression = 1;

    /// <summary>
    /// Offset standing for a null, which has no bytes in the value region to point at
    /// </summary>
    public const ushort NullOffset = 0xFFFE;

    private readonly XpressHuffmanDecoder _decoder = new();

    private ReadOnlyMemory<byte>? _values;

    [DataStructureItem(ItemType.ValuePageSubLobType)]
    public SubLobType SubLobType { get; set; }

    /// <summary>
    /// Byte at 0x04, whose low nibble DBCC CSINDEX reports as the page's compression and high nibble as its flags
    /// </summary>
    [DataStructureItem(ItemType.ValuePageFlags)]
    public byte PageFlags { get; set; }

    public byte Compression => (byte)(PageFlags & 0x0F);

    public byte Flags => (byte)(PageFlags >> 4);

    [DataStructureItem(ItemType.ValuePageReserved)]
    public byte Reserved05 { get; set; }

    /// <summary>
    /// Bytes one value occupies once expanded
    /// </summary>
    [DataStructureItem(ItemType.ValuePageValueSize)]
    public short ValueSize { get; set; }

    [DataStructureItem(ItemType.ValuePageValueCount)]
    public int ValueCount { get; set; }

    [DataStructureItem(ItemType.ValuePagePayloadSize)]
    public ushort PayloadSize { get; set; }

    /// <summary>
    /// Offset of the page within the segment blob
    /// </summary>
    public int Offset { get; set; }

    public int Size { get; set; }

    public ReadOnlyMemory<byte> Compressed { get; set; }

    /// <summary>
    /// Whether a value is wider than the integer the run length encodings work in
    /// </summary>
    public bool IsWide => IsVariableWidth || ValueSize > 8;

    public bool IsCompressed => Compression == HuffmanCompression;

    public ReadOnlyMemory<byte> Values
        => _values ??= IsCompressed ? _decoder.Decode(Compressed, ExpandedSize) : Compressed;

    /// <summary>
    /// Stands in for the payload in a marker, the compressed bytes saying nothing a reader can use
    /// </summary>
    [DataStructureItem(ItemType.ValuePagePayload)]
    public string Payload => IsCompressed ? "[Compressed Payload]" : "[Payload]";

    /// <summary>
    /// Whether the values are not all one width, which the page says by setting the size to all ones
    /// </summary>
    /// <remarks>
    /// Such a page keeps its values from the front and an offset per value at the back, so a value's length is
    /// the distance to the next one rather than a width the page can name.
    /// </remarks>
    public bool IsVariableWidth => ValueSize < 0;

    public int ExpandedSize => IsVariableWidth ? PayloadSize + 1 : ValueCount * ValueSize;

    /// <summary>
    /// Where the offset array starts, being the last two bytes of every value
    /// </summary>
    public int OffsetArrayStart => Values.Length - (ValueCount * 2);

    public int OffsetArraySize => IsVariableWidth ? ValueCount * 2 : 0;

    /// <summary>
    /// Value as stored, for a width the integer path cannot take
    /// </summary>
    public ReadOnlyMemory<byte> GetValueBytes(int index)
    {
        if ((uint)index >= (uint)ValueCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (!IsVariableWidth)
        {
            return Values.Slice(index * ValueSize, ValueSize);
        }

        var offset = GetOffset(index);

        if (offset == NullOffset)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        return Values.Slice(offset, GetEnd(index) - offset);
    }

    /// <summary>
    /// Whether the slot holds a null, which only a variable width page can say and only through its offset array
    /// </summary>
    public bool IsNull(int index) => IsVariableWidth && GetOffset(index) == NullOffset;

    /// <summary>
    /// Offset the array holds for the slot, which is the null marker rather than a position when there is no value
    /// </summary>
    public ushort GetStoredOffset(int index) => GetOffset(index);

    /// <summary>
    /// Where the offset array entry for a slot sits, the array running backwards from the end of the values
    /// </summary>
    public int GetOffsetPosition(int index) => Values.Length - ((index + 1) * 2);

    /// <summary>
    /// Where a value starts within the expanded values, or -1 when the slot holds a null
    /// </summary>
    public int GetValuePosition(int index)
    {
        if (!IsVariableWidth)
        {
            return index * ValueSize;
        }

        return IsNull(index) ? -1 : GetOffset(index);
    }

    public int GetValueLength(int index)
    {
        if (!IsVariableWidth)
        {
            return ValueSize;
        }

        return IsNull(index) ? 0 : GetEnd(index) - GetOffset(index);
    }

    /// <summary>
    /// Where a value's bytes start in the segment blob, or the page itself when they are compressed away
    /// </summary>
    public int GetValueOffset(int index)
    {
        if (IsCompressed)
        {
            return Offset;
        }

        if (!IsVariableWidth)
        {
            return Offset + RawHeaderSize + (index * ValueSize);
        }

        var offset = GetOffset(index);

        return offset == NullOffset ? Offset : Offset + RawHeaderSize + offset;
    }

    public void Mark()
    {
        MarkProperty(nameof(SubLobType), Offset, 4);
        MarkProperty(nameof(PageFlags), Offset + 0x04, 1, [IsCompressed ? "Compressed" : "Uncompressed"]);
        MarkProperty(nameof(Reserved05), Offset + 0x05, 1);
        MarkProperty(nameof(ValueSize), Offset + 0x06, 2);
        MarkProperty(nameof(ValueCount), Offset + 0x08, 4);

        if (IsCompressed)
        {
            MarkProperty(nameof(PayloadSize), Offset + 0x0C, 2);
        }

        var payloadOffset = IsCompressed ? HeaderSize : RawHeaderSize;

        if (Size > payloadOffset)
        {
            MarkProperty(nameof(Payload), Offset + payloadOffset, Size - payloadOffset);
        }
    }

    /// <summary>
    /// Value as stored, being the scaled integer before the reserved low bit is removed
    /// </summary>
    public long GetRawValue(int index)
    {
        if ((uint)index >= (uint)ValueCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var span = Values.Span.Slice(index * ValueSize, ValueSize);

        return ValueSize switch
        {
            8 => BinaryPrimitives.ReadInt64LittleEndian(span),
            4 => BinaryPrimitives.ReadInt32LittleEndian(span),
            2 => BinaryPrimitives.ReadInt16LittleEndian(span),
            1 => (sbyte)span[0],
            _ => throw new InvalidDataException($"Unsupported store by value size {ValueSize}.")
        };
    }

    /// <summary>
    /// Where a value stops, being the next one that has bytes or the offset array if none follows
    /// </summary>
    private int GetEnd(int index)
    {
        for (var next = index + 1; next < ValueCount; next++)
        {
            var offset = GetOffset(next);

            if (offset != NullOffset)
            {
                return offset;
            }
        }

        return OffsetArrayStart;
    }

    /// <summary>
    /// Offset of a value, the array running backwards so the last row is the first entry
    /// </summary>
    private ushort GetOffset(int index)
        => BinaryPrimitives.ReadUInt16LittleEndian(Values.Span.Slice(Values.Length - ((index + 1) * 2), 2));
}
