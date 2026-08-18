using System.Buffers.Binary;
using System.IO;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.Internals.Columnstore.Parsers;

/// <summary>
/// Parses the blob a column segment data pointer resolves to
/// </summary>
public static class SegmentBlobParser
{
    public static SegmentBlob Parse(ReadOnlyMemory<byte> data, bool isMarkEnabled = false)
    {
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
            Version = ReadInt32(span, 0x00),
            Unknown04 = ReadInt32(span, 0x04),
            Unknown08 = ReadInt32(span, 0x08),
            Unknown0C = ReadInt32(span, 0x0C),
            LobType = (ColumnstoreLobType)ReadInt32(span, 0x10),
            BookmarkCount = ReadInt32(span, 0x14),
            BookmarkDistance = ReadInt32(span, 0x18),
            RleArrayCount = ReadInt32(span, 0x1C),
            RleEntrySize = ReadInt16(span, 0x20),
            BitpackEntrySize = ReadInt16(span, 0x22),
            BitpackUnitCount = ReadInt32(span, 0x24),
            BitpackMinId = ReadInt32(span, 0x28),
            Unknown2C = ReadInt32(span, 0x2C)
        };

        MarkHeader(blob);

        if (blob.ExpectedSize != data.Length)
        {
            throw new InvalidDataException($"Segment blob is {data.Length} bytes, header implies {blob.ExpectedSize}.");
        }

        blob.Bookmarks = ReadBookmarks(span, blob);
        blob.RleEntries = ReadRleEntries(span, blob);

        blob.Bitpack = new BitpackArray(data[blob.BitpackArrayOffset..],
                                        blob.BitpackEntrySize,
                                        blob.BitpackUnitCount,
                                        blob.BitpackMinId);

        MarkRegions(blob);

        return blob;
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

    private static RleEntry[] ReadRleEntries(ReadOnlySpan<byte> span, SegmentBlob blob)
    {
        var entries = new RleEntry[blob.RleArrayCount];

        for (var i = 0; i < entries.Length; i++)
        {
            var offset = blob.RleArrayOffset + (i * SegmentBlob.EntrySize);

            entries[i] = new RleEntry(ReadInt32(span, offset), ReadInt32(span, offset + 4));
        }

        return entries;
    }

    private static void MarkHeader(SegmentBlob blob)
    {
        blob.MarkProperty(nameof(SegmentBlob.Version), 0x00, 4);
        blob.MarkProperty(nameof(SegmentBlob.Unknown04), 0x04, 4);
        blob.MarkProperty(nameof(SegmentBlob.Unknown08), 0x08, 4);
        blob.MarkProperty(nameof(SegmentBlob.Unknown0C), 0x0C, 4);
        blob.MarkProperty(nameof(SegmentBlob.LobType), 0x10, 4);
        blob.MarkProperty(nameof(SegmentBlob.BookmarkCount), 0x14, 4);
        blob.MarkProperty(nameof(SegmentBlob.BookmarkDistance), 0x18, 4);
        blob.MarkProperty(nameof(SegmentBlob.RleArrayCount), 0x1C, 4);
        blob.MarkProperty(nameof(SegmentBlob.RleEntrySize), 0x20, 2);
        blob.MarkProperty(nameof(SegmentBlob.BitpackEntrySize), 0x22, 2);
        blob.MarkProperty(nameof(SegmentBlob.BitpackUnitCount), 0x24, 4);
        blob.MarkProperty(nameof(SegmentBlob.BitpackMinId), 0x28, 4);
        blob.MarkProperty(nameof(SegmentBlob.Unknown2C), 0x2C, 4);
    }

    private static void MarkRegions(SegmentBlob blob)
    {
        if (!blob.IsMarkEnabled)
        {
            return;
        }

        blob.MarkValue(ItemType.BookmarkCount,
                       "Bookmark Array",
                       blob.BookmarkCount,
                       blob.BookmarkArrayOffset,
                       blob.BookmarkCount * SegmentBlob.EntrySize);

        blob.MarkValue(ItemType.RleArrayCount,
                       "RLE Array",
                       blob.RleArrayCount,
                       blob.RleArrayOffset,
                       blob.RleArrayCount * SegmentBlob.EntrySize);

        blob.MarkValue(ItemType.BitpackUnitCount,
                       "Bit Pack Array",
                       blob.BitpackUnitCount,
                       blob.BitpackArrayOffset,
                       blob.BitpackUnitCount * SegmentBlob.EntrySize);
    }

    private static int ReadInt32(ReadOnlySpan<byte> span, int offset)
        => BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));

    private static short ReadInt16(ReadOnlySpan<byte> span, int offset)
        => BinaryPrimitives.ReadInt16LittleEndian(span.Slice(offset, 2));
}
