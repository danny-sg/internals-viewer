using System.Buffers.Binary;
using System.IO;
using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Metadata.Enums;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.Internals.Columnstore.Parsers;

/// <summary>
/// Parses a Segment Blob from a raw byte array
/// </summary>
public static class SegmentBlobParser
{
    public const int SupportedVersion = 1;

    public static SegmentBlob Parse(ReadOnlyMemory<byte> data, ColumnSegment? segment, bool isMarkEnabled = false)
    {
        if (ArchiveBlobHeader.IsArchive(data.Span))
        {
            data = ArchiveBlobExpander.Expand(data);
        }

        if (data.Length < SegmentBlob.HeaderSize)
        {
            throw new ArgumentException($"Segment blob is {data.Length} bytes, shorter than the {SegmentBlob.HeaderSize} byte header.",
                                        nameof(data));
        }

        var span = data.Span;

        var blob = new SegmentBlob
        {
            IsMarkEnabled = isMarkEnabled,
            Data = data,
            Segment = segment,
            Header = ParseHeader(span)
        };

        blob.Header.RleEntryBytes = GetRleEntryBytes(segment);

        blob.Header.IsMarkEnabled = blob.IsMarkEnabled;

        blob.Header.Mark();

        if (blob.Header.Version != SupportedVersion)
        {
            throw new InvalidDataException($"Segment blob version {blob.Header.Version} is not supported.");
        }

        blob.Bookmarks = ReadBookmarks(span, blob);

        if (blob.Header.IsVariableLengthData)
        {
            blob.VariableLengthData = ReadVariableLengthData(data, blob);

            blob.RleEntries = ReadRleEntries(span, blob);

            return blob;
        }

        if (blob.Header.RleType != SegmentRleType.BitPack)
        {
            throw new InvalidDataException($"Segment structure type {(int)blob.Header.RleType} is not supported.");
        }

        if (blob.Header.ExpectedSize != data.Length)
        {
            throw new InvalidDataException($"Segment blob is {data.Length} bytes, header implies {blob.Header.ExpectedSize}.");
        }

        blob.RleEntries = ReadRleEntries(span, blob);

        blob.Bitpack = new BitpackArray(data[blob.Header.BitpackArrayOffset..],
                                        blob.Header.BitpackEntrySize,
                                        blob.Header.BitpackUnitCount,
                                        blob.Header.BitpackMinId);

        return blob;
    }

    /// <summary>
    /// Reads the prologue on its own, which describes the layout without any of the arrays being present
    /// </summary>
    public static SegmentBlobHeader ParseHeader(ReadOnlySpan<byte> span)
    {
        if (span.Length < SegmentBlobHeader.Size)
        {
            throw new ArgumentException($"Segment blob header needs {SegmentBlobHeader.Size} bytes, {span.Length} given.",
                                        nameof(span));
        }

        return new SegmentBlobHeader
        {
            Version = ReadInt32(span, 0x00),
            LobType = (ColumnstoreLobType)ReadInt32(span, 0x04),
            Reserved = ReadInt32(span, 0x08),
            Unknown0C = ReadInt32(span, 0x0C),
            RleType = (SegmentRleType)ReadInt32(span, 0x10),
            BookmarkCount = ReadInt32(span, 0x14),
            BookmarkDistance = ReadInt32(span, 0x18),
            RleArrayCount = ReadInt32(span, 0x1C),
            RleEntrySize = ReadInt16(span, 0x20),
            BitpackEntrySize = ReadInt16(span, 0x22),
            BitpackUnitCount = ReadInt32(span, 0x24),
            BitpackMinId = ReadInt64(span, 0x28)
        };
    }

    /// <summary>
    /// Reads the bookmark array, whose declared count runs two entries into the RLE array on a VLD segment
    /// </summary>
    /// <remarks>
    /// The RLE array starts sixteen bytes before `BookmarkArrayOffset + BookmarkCount * 8`, so the last two slots
    /// hold the first two RLE entries.
    /// </remarks>
    private static SegmentBookmark[] ReadBookmarks(ReadOnlySpan<byte> span, SegmentBlob blob)
    {
        var count = blob.Header.IsVariableLengthData
                                ? Math.Max(0, blob.Header.BookmarkCount - 2)
                                : blob.Header.BookmarkCount;

        var bookmarks = new SegmentBookmark[count];

        for (var i = 0; i < bookmarks.Length; i++)
        {
            var offset = blob.Header.BookmarkArrayOffset + (i * SegmentBlob.EntrySize);

            bookmarks[i] = new SegmentBookmark(ReadInt32(span, offset), ReadInt32(span, offset + 4));
        }

        return bookmarks;
    }

    private static SegmentVariableLengthData ReadVariableLengthData(ReadOnlyMemory<byte> data, SegmentBlob blob)
    {
        var span = data.Span;

        var offset = blob.Header.VariableLengthDataOffset;

        var store = new SegmentVariableLengthData
        {
            Header = new SegmentVariableLengthDataHeader
            {
                Offset = offset,
                IsMarkEnabled = blob.IsMarkEnabled,
                SubLobType = (SubLobType)ReadInt32(span, offset),
                ValueCount = ReadInt32(span, offset + 0x04),
                MaxStringSize = ReadInt32(span, offset + 0x08)
            },
            PageSizeArray = new SegmentPageSizeArray
            {
                Offset = offset + SegmentVariableLengthDataHeader.Size,
                IsMarkEnabled = blob.IsMarkEnabled,
                SubLobType = (SubLobType)ReadInt32(span, offset + 0x0C),
                ElementSize = ReadInt32(span, offset + 0x10),
                ElementCount = ReadInt32(span, offset + 0x14)
            },
            Offset = offset,
            IsMarkEnabled = blob.IsMarkEnabled
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

            pages[i].IsMarkEnabled = blob.IsMarkEnabled;

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

    /// <summary>
    /// Width of an RLE entry, taken from how large a data id the segment has to hold
    /// </summary>
    /// <remarks>
    /// A literal run carries the data id relative to the base, in a signed field whose negatives mean a read run,
    /// so an id past int.MaxValue cannot fit and the entry doubles. A dictionary segment leaves base and magnitude
    /// at -1 and holds slot numbers, which are always small. Measured against 400 segments, 32 of them wide.
    /// </remarks>
    private static int GetRleEntryBytes(ColumnSegment? segment)
    {
        if (segment is null)
        {
            return SegmentBlob.EntrySize;
        }

        var storedMax = segment.Encoding == SegmentEncoding.StoreByValueBased
            ? segment.MaxDataId
            : segment.BaseId >= 0 && segment.Magnitude > 0
                ? (segment.MaxDataId / segment.Magnitude) - segment.BaseId
                : 0;

        return storedMax > int.MaxValue ? SegmentBlob.EntrySize * 2 : SegmentBlob.EntrySize;
    }

    private static RleEntry[] ReadRleEntries(ReadOnlySpan<byte> span, SegmentBlob blob)
    {
        var entries = ReadRleEntries(span, blob, blob.Header.RleEntryBytes);

        // A count cannot be negative, so a value split in half by the wrong width says so rather than passing quietly
        if (Array.Exists(entries, e => e.Count < 0))
        {
            throw new InvalidDataException($"Segment RLE array read as {blob.Header.RleEntryBytes} byte entries "
                                           + "gives a negative run count, so the entry width is wrong.");
        }

        return entries;
    }

    private static RleEntry[] ReadRleEntries(ReadOnlySpan<byte> span, SegmentBlob blob, int entryBytes)
    {
        var entries = new RleEntry[blob.Header.RleArrayCount * SegmentBlob.EntrySize / entryBytes];

        var wide = entryBytes > SegmentBlob.EntrySize;

        for (var i = 0; i < entries.Length; i++)
        {
            var offset = blob.Header.RleArrayOffset + (i * entryBytes);

            entries[i] = wide
                ? new RleEntry(ReadInt64(span, offset), ReadInt32(span, offset + 8), blob.Header.IsVariableLengthData)
                : new RleEntry(ReadInt32(span, offset), ReadInt32(span, offset + 4), blob.Header.IsVariableLengthData);
        }

        return entries;
    }

    private static int ReadInt32(ReadOnlySpan<byte> span, int offset)
        => BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));

    private static long ReadInt64(ReadOnlySpan<byte> span, int offset)
        => BinaryPrimitives.ReadInt64LittleEndian(span.Slice(offset, 8));

    private static short ReadInt16(ReadOnlySpan<byte> span, int offset)
        => BinaryPrimitives.ReadInt16LittleEndian(span.Slice(offset, 2));
}
