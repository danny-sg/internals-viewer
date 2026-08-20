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

        MarkHeader(blob);

        if (blob.Version != SupportedVersion)
        {
            throw new InvalidDataException($"Segment blob version {blob.Version} is not supported.");
        }

        blob.Bookmarks = ReadBookmarks(span, blob);

        if (blob.IsStoreByValue)
        {
            blob.ValueStore = ReadValueStore(data, blob);

            return blob;
        }

        if (blob.StructureType != SegmentStructureType.RunLength)
        {
            throw new InvalidDataException($"Segment structure type {(int)blob.StructureType} is not supported.");
        }

        if (blob.ExpectedSize != data.Length)
        {
            throw new InvalidDataException($"Segment blob is {data.Length} bytes, header implies {blob.ExpectedSize}.");
        }

        blob.RleEntries = ReadRleEntries(span, blob);

        blob.Bitpack = new BitpackArray(data[blob.BitpackArrayOffset..],
                                        blob.BitpackEntrySize,
                                        blob.BitpackUnitCount,
                                        blob.BitpackMinId);

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
        var bookmarks = new SegmentBookmark[blob.BookmarkCount];

        for (var i = 0; i < bookmarks.Length; i++)
        {
            var offset = blob.BookmarkArrayOffset + (i * SegmentBlob.EntrySize);

            bookmarks[i] = new SegmentBookmark(ReadInt32(span, offset), ReadInt32(span, offset + 4));
        }

        return bookmarks;
    }

    private static SegmentValueStore ReadValueStore(ReadOnlyMemory<byte> data, SegmentBlob blob)
    {
        var span = data.Span;

        var offset = blob.ValueStoreOffset;

        var store = new SegmentValueStore
        {
            Unknown00 = ReadInt32(span, offset),
            ValueCount = ReadInt32(span, offset + 0x04),
            MaxStringSize = ReadInt32(span, offset + 0x08),
            SubLobType = (SubLobType)ReadInt32(span, offset + 0x0C),
            ElementSize = ReadInt32(span, offset + 0x10),
            PageCount = ReadInt32(span, offset + 0x14),
            Offset = offset,
            IsMarkEnabled = blob.IsMarkEnabled
        };

        var pageCount = store.PageCount;

        offset += SegmentValueStore.HeaderSize;

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
        store.Pages = pages;

        store.Mark();

        return store;
    }

    private static SegmentValuePage ReadValuePage(ReadOnlyMemory<byte> data, int offset, int size)
    {
        var span = data.Span;

        return new SegmentValuePage
        {
            SubLobType = (SubLobType)ReadInt32(span, offset),
            Unknown04 = ReadInt16(span, offset + 0x04),
            ValueSize = ReadInt16(span, offset + 0x06),
            ValueCount = ReadInt32(span, offset + 0x08),
            PayloadSize = (ushort)ReadInt16(span, offset + 0x0C),
            Offset = offset,
            Size = size,
            Compressed = data.Slice(offset + SegmentValuePage.HeaderSize, size - SegmentValuePage.HeaderSize)
        };
    }

    private static RleEntry[] ReadRleEntries(ReadOnlySpan<byte> span, SegmentBlob blob)
    {
        var entries = new RleEntry[blob.RleEntryCount];

        var wide = blob.RleEntryBytes > SegmentBlob.EntrySize;

        for (var i = 0; i < entries.Length; i++)
        {
            var offset = blob.RleArrayOffset + (i * blob.RleEntryBytes);

            entries[i] = wide
                ? new RleEntry(ReadInt64(span, offset), ReadInt32(span, offset + 8))
                : new RleEntry(ReadInt32(span, offset), ReadInt32(span, offset + 4));
        }

        return entries;
    }

    private static void MarkHeader(SegmentBlob blob)
    {
        blob.MarkProperty(nameof(SegmentBlob.Version), 0x00, 4);
        blob.MarkProperty(nameof(SegmentBlob.LobType), 0x04, 4);
        blob.MarkProperty(nameof(SegmentBlob.Reserved), 0x08, 4);
        blob.MarkProperty(nameof(SegmentBlob.Unknown0C), 0x0C, 4);
        blob.MarkProperty(nameof(SegmentBlob.StructureType), 0x10, 4);
        blob.MarkProperty(nameof(SegmentBlob.BookmarkCount), 0x14, 4);
        blob.MarkProperty(nameof(SegmentBlob.BookmarkDistance), 0x18, 4);
        blob.MarkProperty(nameof(SegmentBlob.RleArrayCount), 0x1C, 4);
        blob.MarkProperty(nameof(SegmentBlob.RleEntrySize), 0x20, 2);
        blob.MarkProperty(nameof(SegmentBlob.BitpackEntrySize), 0x22, 2);
        blob.MarkProperty(nameof(SegmentBlob.BitpackUnitCount), 0x24, 4);
        blob.MarkProperty(nameof(SegmentBlob.BitpackMinId), 0x28, 8);
    }

    private static int ReadInt32(ReadOnlySpan<byte> span, int offset)
        => BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));

    private static long ReadInt64(ReadOnlySpan<byte> span, int offset)
        => BinaryPrimitives.ReadInt64LittleEndian(span.Slice(offset, 8));

    private static short ReadInt16(ReadOnlySpan<byte> span, int offset)
        => BinaryPrimitives.ReadInt16LittleEndian(span.Slice(offset, 2));
}
