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
    public const int HeaderSize = 14;

    private readonly XpressHuffmanDecoder _decoder = new();

    private ReadOnlyMemory<byte>? _values;

    [DataStructureItem(ItemType.ValuePageSubLobType)]
    public SubLobType SubLobType { get; set; }

    [DataStructureItem(ItemType.ValuePageUnknown)]
    public short Unknown04 { get; set; }

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
    /// Stands in for the payload in a marker, the compressed bytes saying nothing a reader can use
    /// </summary>
    [DataStructureItem(ItemType.ValuePagePayload)]
    public static string Payload => "[Compressed Payload]";

    public int ExpandedSize => ValueCount * ValueSize;

    public ReadOnlyMemory<byte> Values => _values ??= _decoder.Decode(Compressed, ExpandedSize);

    public void Mark()
    {
        MarkProperty(nameof(SubLobType), Offset, 4);
        MarkProperty(nameof(Unknown04), Offset + 0x04, 2);
        MarkProperty(nameof(ValueSize), Offset + 0x06, 2);
        MarkProperty(nameof(ValueCount), Offset + 0x08, 4);
        MarkProperty(nameof(PayloadSize), Offset + 0x0C, 2);

        if (Size > HeaderSize)
        {
            MarkProperty(nameof(Payload), Offset + HeaderSize, Size - HeaderSize);
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
}
