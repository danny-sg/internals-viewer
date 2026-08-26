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
/// <remarks>
/// Segment parsing is as follows:
///
/// 1. Check if the Segment is ARCHIVE_COMPRESSION - signified by ArchiveBlobHeader.IsArchive
///    - If it is compressed, data decompressed and parsing continues
///
/// 2. Header is parsed including:
///
///     0x00 - Version - Int32
///     0x04 - LobType - Int32
///     0x08 - Reserved - Int32
///     0x0C - Unknown - Int32
///     0x10 - RleType - Int32
///     0x14 - BookmarkCount - Int32
///     0x18 - BookmarkDistance - Int32
///     0x1C - RleArrayCount - Int32
///     0x20 - RleArrayEntrySize - Int16
///     0x22 - BitpackEntrySize - Int16
///     0x24 - BitpackUnitCount - Int32
///     0x28 - BitpackMinId - Int64
/// 
/// </remarks>
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

        blob.Header.RleValueSize = CalculateRleValueSize(segment);

        blob.Header.IsMarkEnabled = blob.IsMarkEnabled;

        blob.Header.Mark();

        if (blob.Header.Version != SupportedVersion)
        {
            throw new InvalidDataException($"Segment blob version {blob.Header.Version} is not supported.");
        }

        blob.Bookmarks = ReadBookmarks(span, blob);

        if (blob.Header.IsVariableLengthData)
        {
            blob.VariableLengthData = SegmentVariableLengthDataParser.Parse(data,
                                                                            blob.Header.VariableLengthDataOffset,
                                                                            blob.IsMarkEnabled);

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
            RleArrayEntrySize = ReadInt16(span, 0x20),
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
        var bookmarks = new SegmentBookmark[blob.Header.BookmarkEntryCount];

        for (var i = 0; i < bookmarks.Length; i++)
        {
            var offset = blob.Header.BookmarkArrayOffset + (i * SegmentBlob.EntrySize);

            bookmarks[i] = new SegmentBookmark(ReadInt32(span, offset), ReadInt32(span, offset + 4));
        }

        return bookmarks;
    }

    /// <summary>
    /// Width of an RLE entry, taken from how large a data id the segment has to hold
    /// </summary>
    /// <remarks>
    /// A literal run carries the data id relative to the base, in a signed field whose negatives mean a read run,
    /// so an id past int.MaxValue cannot fit and the entry doubles. A dictionary segment leaves base and magnitude
    /// at -1 and holds slot numbers, which are always small. Measured against 400 segments, 32 of them wide.
    /// </remarks>
    private static int CalculateRleValueSize(ColumnSegment? segment)
    {
        if (segment is null)
        {
            return sizeof(int);
        }

        var storedMax = segment.Encoding == SegmentEncoding.StoreByValueBased
            ? segment.MaxDataId
            : segment is { BaseId: >= 0, Magnitude: > 0 }
                ? (segment.MaxDataId / segment.Magnitude) - segment.BaseId
                : 0;

        return storedMax > int.MaxValue ? sizeof(long) : sizeof(int);
    }

    private static RleEntry[] ReadRleEntries(ReadOnlySpan<byte> span, SegmentBlob blob)
    {
        var entries = ReadRleEntries(span, blob, blob.Header.RleEntrySize);

        // A count cannot be negative, so a value split in half by the wrong width says so rather than passing quietly
        if (Array.Exists(entries, e => e.Count < 0))
        {
            throw new InvalidDataException($"Segment RLE array read as {blob.Header.RleEntrySize} byte entries "
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
                ? new RleEntry(ReadInt64(span, offset),
                               ReadInt32(span, offset + 8),
                               blob.Header.IsVariableLengthData,
                               ReadInt32(span, offset + 12))
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
