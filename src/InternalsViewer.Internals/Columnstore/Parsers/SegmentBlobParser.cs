using System.Buffers.Binary;
using System.IO;
using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.Internals.Columnstore.Parsers;

/// <summary>
/// Parses a Segment Blob from a raw byte array
/// </summary>
public static class SegmentBlobParser
{
    public const int SupportedVersion = 1;

    public static SegmentBlob Parse(ReadOnlyMemory<byte> data, bool isMarkEnabled = false)
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
            Header = ParseHeader(span)
        };

        blob.Header.IsMarkEnabled = blob.IsMarkEnabled;

        blob.Header.Mark();

        if (blob.Header.Version != SupportedVersion)
        {
            throw new InvalidDataException($"Segment blob version {blob.Header.Version} is not supported.");
        }

        blob.Bookmarks = ReadBookmarks(span, blob);

        if (blob.Header.IsStoreByValue)
        {
            blob.VariableLengthData = ReadVariableLengthData(data, blob);

            return blob;
        }

        if (blob.Header.StructureType != SegmentStructureType.RunLength)
        {
            throw new InvalidDataException($"Segment structure type {(int)blob.Header.StructureType} is not supported.");
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
            StructureType = (SegmentStructureType)ReadInt32(span, 0x10),
            BookmarkCount = ReadInt32(span, 0x14),
            BookmarkDistance = ReadInt32(span, 0x18),
            RleArrayCount = ReadInt32(span, 0x1C),
            RleEntrySize = ReadInt16(span, 0x20),
            BitpackEntrySize = ReadInt16(span, 0x22),
            BitpackUnitCount = ReadInt32(span, 0x24),
            BitpackMinId = ReadInt64(span, 0x28)
        };
    }

    private static SegmentBookmark[] ReadBookmarks(ReadOnlySpan<byte> span, SegmentBlob blob)
    {
        var bookmarks = new SegmentBookmark[blob.Header.BookmarkCount];

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

        // Only a store whose pages all carry an offset array has somewhere to put a null, so only that one has a
        // value for every row
        store.IsRowAligned = pages.Length == 0 || pages.All(p => p.IsVariableWidth) || pages.All(p => !p.IsVariableWidth);

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

    private static RleEntry[] ReadRleEntries(ReadOnlySpan<byte> span, SegmentBlob blob)
    {
        var entries = new RleEntry[blob.Header.RleEntryCount];

        var wide = blob.Header.RleEntryBytes > SegmentBlob.EntrySize;

        for (var i = 0; i < entries.Length; i++)
        {
            var offset = blob.Header.RleArrayOffset + (i * blob.Header.RleEntryBytes);

            entries[i] = wide
                ? new RleEntry(ReadInt64(span, offset), ReadInt32(span, offset + 8))
                : new RleEntry(ReadInt32(span, offset), ReadInt32(span, offset + 4));
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
