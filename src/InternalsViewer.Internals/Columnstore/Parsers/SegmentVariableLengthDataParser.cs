using System.Buffers.Binary;
using System.IO;
using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.Internals.Columnstore.Parsers;

/// <summary>
/// Parses the variable length data store a store by value segment carries after its RLE array
/// </summary>
public static class SegmentVariableLengthDataParser
{
    public static SegmentVariableLengthData Parse(ReadOnlyMemory<byte> data, int offset, bool isMarkEnabled)
    {
        var span = data.Span;

        var store = new SegmentVariableLengthData
        {
            Header = new SegmentVariableLengthDataHeader
            {
                Offset = offset,
                IsMarkEnabled = isMarkEnabled,
                SubLobType = (SubLobType)ReadInt32(span, offset),
                ValueCount = ReadInt32(span, offset + 0x04),
                MaxStringSize = ReadInt32(span, offset + 0x08)
            },
            PageSizeArray = new SegmentPageSizeArray
            {
                Offset = offset + SegmentVariableLengthDataHeader.Size,
                IsMarkEnabled = isMarkEnabled,
                SubLobType = (SubLobType)ReadInt32(span, offset + 0x0C),
                ElementSize = ReadInt32(span, offset + 0x10),
                ElementCount = ReadInt32(span, offset + 0x14)
            },
            Offset = offset,
            IsMarkEnabled = isMarkEnabled
        };

        var pageCount = store.PageCount;

        offset += SegmentVariableLengthData.HeaderSize;

        var sizes = new int[pageCount];

        for (var i = 0; i < pageCount; i++)
        {
            sizes[i] = ReadInt32(span, offset + (i * store.ElementSize));
        }

        offset += pageCount * store.ElementSize;

        var pages = new SegmentValuePage[pageCount];

        for (var i = 0; i < pageCount; i++)
        {
            pages[i] = ReadValuePage(data, offset, sizes[i]);

            pages[i].IsMarkEnabled = isMarkEnabled;

            pages[i].Mark();

            offset += sizes[i];
        }

        if (offset != data.Length)
        {
            throw new InvalidDataException($"Store by value pages end at {offset}, blob is {data.Length} bytes.");
        }

        store.PageSizes = sizes;
        store.PageSizeArray.PageSizes = sizes;
        store.Pages = pages;

        store.Mark();

        return store;
    }

    private static SegmentValuePage ReadValuePage(ReadOnlyMemory<byte> data, int offset, int size)
    {
        var span = data.Span;

        var subLobType = (SubLobType)ReadInt32(span, offset);

        // The payload is read as Xpress Huffman whatever the type says, so an unknown one has to stop here rather
        // than decompress into nonsense the way it would if the type were simply ignored
        if (subLobType != SubLobType.ValuePage)
        {
            throw new InvalidDataException($"Store by value page sub lob type {(int)subLobType} is not supported.");
        }

        var compression = (byte)(span[offset + 0x04] & 0x0F);

        // A page whose values do not compress is held raw, and carries no payload size to make room for
        var headerSize = compression == SegmentValuePage.HuffmanCompression
                         ? SegmentValuePage.HeaderSize
                         : SegmentValuePage.RawHeaderSize;

        return new SegmentValuePage
        {
            SubLobType = subLobType,
            PageFlags = span[offset + 0x04],
            Reserved05 = span[offset + 0x05],
            ValueSize = ReadInt16(span, offset + 0x06),
            ValueCount = ReadInt32(span, offset + 0x08),
            PayloadSize = headerSize == SegmentValuePage.HeaderSize ? (ushort)ReadInt16(span, offset + 0x0C) : (ushort)0,
            Offset = offset,
            Size = size,
            Compressed = data.Slice(offset + headerSize, size - headerSize)
        };
    }

    private static int ReadInt32(ReadOnlySpan<byte> span, int offset)
        => BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));

    private static short ReadInt16(ReadOnlySpan<byte> span, int offset)
        => BinaryPrimitives.ReadInt16LittleEndian(span.Slice(offset, 2));
}
