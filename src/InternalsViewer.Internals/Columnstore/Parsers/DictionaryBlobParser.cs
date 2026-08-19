using System.Buffers.Binary;
using System.IO;
using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.Internals.Columnstore.Parsers;

/// <summary>
/// Parses the blob a dictionary data pointer resolves to
/// </summary>
public static class DictionaryBlobParser
{
    public static DictionaryBlob Parse(ReadOnlyMemory<byte> data, int entryCount, int lastId, bool isMarkEnabled = false)
    {
        if (ArchiveBlobHeader.IsArchive(data.Span))
        {
            data = ArchiveBlobExpander.Expand(data);
        }

        var span = data.Span;

        var lobType = (ColumnstoreLobType)ReadInt32(span, 0x04);

        var firstId = lastId - entryCount + 1;

        return lobType switch
        {
            ColumnstoreLobType.NumericDictionary 
                => ParseNumeric(data, entryCount, firstId, isMarkEnabled),
            ColumnstoreLobType.Segment 
                => ParseString(data, entryCount, firstId, isMarkEnabled),
            _ => throw new InvalidDataException($"Unsupported dictionary lob type {(int)lobType}.")
        };
    }

    private static NumericDictionary ParseNumeric(ReadOnlyMemory<byte> data, int entryCount, int firstId, bool isMarkEnabled)
    {
        var span = data.Span;

        var dictionary = new NumericDictionary
        {
            IsMarkEnabled = isMarkEnabled,
            Data = data,
            Version = ReadInt32(span, 0x00),
            LobType = (ColumnstoreLobType)ReadInt32(span, 0x04),
            Reserved = ReadInt32(span, 0x08),
            EntryCount = entryCount,
            FirstId = firstId,
            SubLobType = (SubLobType)ReadInt32(span, 0x0C),
            BucketSize = ReadInt32(span, 0x10),
            BucketCount = ReadInt32(span, 0x14),
            MaxLocalEntryCount = ReadInt32(span, 0x18),
            HashEntrySize = ReadInt32(span, 0x1C),
            HashEntryCount = ReadInt32(span, 0x20),
            CollisionCount = ReadInt32(span, 0x24),
            BucketIndexMask = (uint)ReadInt32(span, 0x28),
            ValueSubLobType = (SubLobType)ReadInt32(span, 0x2C),
            ElementSize = ReadInt32(span, 0x30),
            ValueCount = ReadInt32(span, 0x34)
        };

        MarkNumericHeader(dictionary);

        var count = dictionary.ValueCount;

        var values = new long[count];

        for (var i = 0; i < count; i++)
        {
            values[i] = ReadValue(span, NumericDictionary.HeaderSize + (i * dictionary.ElementSize), dictionary.ElementSize);
        }

        dictionary.Values = values;

        return dictionary;
    }

    private static StringDictionary ParseString(ReadOnlyMemory<byte> data, int entryCount, int firstId, bool isMarkEnabled)
    {
        var span = data.Span;

        var dictionary = new StringDictionary
        {
            IsMarkEnabled = isMarkEnabled,
            Data = data,
            Version = ReadInt32(span, 0x00),
            LobType = (ColumnstoreLobType)ReadInt32(span, 0x04),
            EntryCount = entryCount,
            FirstId = firstId,
            Reserved = ReadInt32(span, 0x08),
            SubLobType = (SubLobType)ReadInt32(span, 0x0C),
            StringCount = ReadInt32(span, 0x10),
            MaxStringSize = ReadInt32(span, 0x14),
            Reserved18 = data.Slice(0x18, 0x24).ToArray(),
            Unknown44 = ReadInt32(span, 0x44),
            Unknown48 = ReadInt32(span, 0x48),
            HandleSize = ReadInt32(span, 0x3C),
            HandleCount = ReadInt32(span, 0x40),
            PageCount = ReadInt32(span, 0x4C)
        };

        MarkStringHeader(dictionary);

        var handleCount = dictionary.HandleCount;

        var pageCount = dictionary.PageCount;

        var offset = StringDictionary.HandleArrayOffset;

        var handles = new StringHandle[handleCount];

        for (var i = 0; i < handleCount; i++)
        {
            handles[i] = new StringHandle(ReadInt32(span, offset), ReadInt32(span, offset + 4));

            offset += dictionary.HandleSize;
        }

        var pageSizes = new int[pageCount];

        for (var i = 0; i < pageCount; i++)
        {
            pageSizes[i] = ReadInt32(span, offset);

            offset += 4;
        }

        var pages = new StringPage[pageCount];

        for (var i = 0; i < pageCount; i++)
        {
            pages[i] = ParsePage(data, offset, pageSizes[i]);

            pages[i].IsMarkEnabled = dictionary.IsMarkEnabled;

            pages[i].Mark();

            offset += pageSizes[i];
        }

        dictionary.Handles = handles;
        dictionary.PageSizes = pageSizes;
        dictionary.Pages = pages;

        return dictionary;
    }

    private static StringPage ParsePage(ReadOnlyMemory<byte> data, int offset, int size)
    {
        var span = data.Span;

        var subLobType = (SubLobType)ReadInt32(span, offset);

        return subLobType switch
        {
            SubLobType.StringPage 
                => new UncompressedStringPage
                {
                    SubLobType = subLobType,
                    Offset = offset,
                    Size = size,
                    PageFlags = ReadInt32(span, offset + 0x04),
                    StringCount = ReadInt32(span, offset + 0x08),
                    FreeSpace = ReadInt32(span, offset + 0x0C),
                    FreeSpaceOffset = ReadInt32(span, offset + 0x10),
                    UncompressedDataSize = ReadInt32(span, offset + 0x14),
                    Content = data.Slice(offset + UncompressedStringPage.HeaderSize, size - UncompressedStringPage.HeaderSize)
                },
            SubLobType.CompressedStringPage 
                => BuildHuffmanPage(data, offset, size),
            _ => throw new InvalidDataException($"Unsupported string page sub lob type {(int)subLobType}.")
        };
    }

    private static HuffmanStringPage BuildHuffmanPage(ReadOnlyMemory<byte> data, int offset, int size)
    {
        var span = data.Span;

        var page = new HuffmanStringPage
        {
            SubLobType = SubLobType.CompressedStringPage,
            Offset = offset,
            Size = size,
            PageFlags = ReadInt32(span, offset + 0x04),
            StringCount = ReadInt32(span, offset + 0x08),
            HuffmanBlobType = ReadInt32(span, offset + 0x0C),
            BitCount = ReadInt32(span, offset + 0x10),
            DecoderBitSize = ReadInt32(span, offset + 0x14),
            CompressedDataSize = ReadInt32(span, offset + 0x18),
            CharacterSetCode = span[offset + 0x1C],
            CodeLengths = data.Slice(offset + HuffmanStringPage.HeaderSize, HuffmanStringPage.CodeLengthTableSize),
            Alignment = data.Slice(offset + HuffmanStringPage.HeaderSize + HuffmanStringPage.CodeLengthTableSize,
                                   HuffmanStringPage.DataOffset - HuffmanStringPage.HeaderSize - HuffmanStringPage.CodeLengthTableSize),
            Content = data.Slice(offset + HuffmanStringPage.DataOffset, size - HuffmanStringPage.DataOffset)
        };

        page.Build();

        return page;
    }

    private static long ReadValue(ReadOnlySpan<byte> span, int offset, int elementSize) => elementSize switch
    {
        1 => span[offset],
        2 => BinaryPrimitives.ReadInt16LittleEndian(span.Slice(offset, 2)),
        4 => BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4)),
        8 => BinaryPrimitives.ReadInt64LittleEndian(span.Slice(offset, 8)),
        _ => throw new InvalidDataException($"Unsupported dictionary element size {elementSize}.")
    };

    private static void MarkStringHeader(StringDictionary dictionary)
    {
        dictionary.MarkProperty(nameof(StringDictionary.Version), 0x00, 4);
        dictionary.MarkProperty(nameof(StringDictionary.LobType), 0x04, 4);
        dictionary.MarkProperty(nameof(StringDictionary.Reserved), 0x08, 4);
        dictionary.MarkProperty(nameof(StringDictionary.SubLobType), 0x0C, 4);
        dictionary.MarkProperty(nameof(StringDictionary.StringCount), 0x10, 4);
        dictionary.MarkProperty(nameof(StringDictionary.MaxStringSize), 0x14, 4);
        dictionary.MarkProperty(nameof(StringDictionary.HandleSize), 0x3C, 4);
        dictionary.MarkProperty(nameof(StringDictionary.HandleCount), 0x40, 4);
        dictionary.MarkProperty(nameof(StringDictionary.Reserved18), 0x18, 0x24);
        dictionary.MarkProperty(nameof(StringDictionary.Unknown44), 0x44, 4);
        dictionary.MarkProperty(nameof(StringDictionary.Unknown48), 0x48, 4);
        dictionary.MarkProperty(nameof(StringDictionary.PageCount), 0x4C, 4);
    }

    private static void MarkNumericHeader(NumericDictionary dictionary)
    {
        dictionary.MarkProperty(nameof(NumericDictionary.Version), 0x00, 4);
        dictionary.MarkProperty(nameof(NumericDictionary.LobType), 0x04, 4);
        dictionary.MarkProperty(nameof(NumericDictionary.Reserved), 0x08, 4);
        dictionary.MarkProperty(nameof(NumericDictionary.SubLobType), 0x0C, 4);
        dictionary.MarkProperty(nameof(NumericDictionary.BucketSize), 0x10, 4);
        dictionary.MarkProperty(nameof(NumericDictionary.BucketCount), 0x14, 4);
        dictionary.MarkProperty(nameof(NumericDictionary.MaxLocalEntryCount), 0x18, 4);
        dictionary.MarkProperty(nameof(NumericDictionary.HashEntrySize), 0x1C, 4);
        dictionary.MarkProperty(nameof(NumericDictionary.HashEntryCount), 0x20, 4);
        dictionary.MarkProperty(nameof(NumericDictionary.CollisionCount), 0x24, 4);
        dictionary.MarkProperty(nameof(NumericDictionary.BucketIndexMask), 0x28, 4);
        dictionary.MarkProperty(nameof(NumericDictionary.ValueSubLobType), 0x2C, 4);
        dictionary.MarkProperty(nameof(NumericDictionary.ElementSize), 0x30, 4);
        dictionary.MarkProperty(nameof(NumericDictionary.ValueCount), 0x34, 4);
    }

    private static int ReadInt32(ReadOnlySpan<byte> span, int offset)
        => BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));
}
