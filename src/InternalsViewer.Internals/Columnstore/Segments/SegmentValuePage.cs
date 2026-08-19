using System.Buffers.Binary;
using System.IO;
using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Compression;

namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// Page of a store by value segment, holding a fixed width value array under Xpress Huffman
/// </summary>
public sealed class SegmentValuePage
{
    public const int HeaderSize = 14;

    private readonly XpressHuffmanDecoder _decoder = new();

    private ReadOnlyMemory<byte>? _values;

    public SubLobType SubLobType { get; set; }

    public short Unknown04 { get; set; }

    /// <summary>
    /// Bytes one value occupies once expanded
    /// </summary>
    public short ValueSize { get; set; }

    public int ValueCount { get; set; }

    public ushort PayloadSize { get; set; }

    /// <summary>
    /// Offset of the page within the segment blob
    /// </summary>
    public int Offset { get; set; }

    public int Size { get; set; }

    public ReadOnlyMemory<byte> Compressed { get; set; }

    public int ExpandedSize => ValueCount * ValueSize;

    public ReadOnlyMemory<byte> Values => _values ??= _decoder.Decode(Compressed, ExpandedSize);

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
