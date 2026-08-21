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
            ColumnstoreLobType.StringDictionary 
                => ParseString(data, entryCount, firstId, isMarkEnabled),
            _ => throw new InvalidDataException($"Unsupported dictionary lob type {(int)lobType}.")
        };
    }

    /// <summary>
    /// Reads the sub lob type of the first page from a prefix, which is how a dictionary's coding is known cheaply
    /// </summary>
    /// <remarks>
    /// Pages sit after the handle and page size arrays, so how far in the first one starts depends on how many
    /// entries the dictionary holds. The caller reads the header, asks for the offset, then reads that far.
    /// </remarks>
    public static int? GetFirstPageOffset(ReadOnlyMemory<byte> data)
    {
        var span = data.Span;

        if (span.Length < StringDictionary.HandleArrayOffset
            || (ColumnstoreLobType)ReadInt32(span, 0x04) != ColumnstoreLobType.StringDictionary)
        {
            return null;
        }

        var handleSize = ReadInt32(span, StringDictionary.HandleArrayHeaderOffset + 0x04);

        var handleCount = ReadInt32(span, StringDictionary.HandleArrayHeaderOffset + 0x08);

        var pageCount = ReadInt32(span, StringDictionary.PageSizeArrayHeaderOffset + 0x08);

        if (handleSize <= 0 || handleCount < 0 || pageCount <= 0)
        {
            return null;
        }

        return StringDictionary.HandleArrayOffset + (handleCount * handleSize) + (pageCount * PageSizeBytes);
    }

    public static SubLobType? ParsePageCoding(ReadOnlyMemory<byte> data, int offset)
    {
        var span = data.Span;

        return span.Length < offset + 4 ? null : (SubLobType)ReadInt32(span, offset);
    }

    private const int PageSizeBytes = 4;

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
            HashTable = new NumericDictionaryHashTable
            {
                IsMarkEnabled = isMarkEnabled,
                SubLobType = (SubLobType)ReadInt32(span, 0x0C),
                BucketSize = ReadInt32(span, 0x10),
                BucketCount = ReadInt32(span, 0x14),
                MaxLocalEntryCount = ReadInt32(span, 0x18),
                EntrySize = ReadInt32(span, 0x1C),
                EntryCount = ReadInt32(span, 0x20),
                CollisionCount = ReadInt32(span, 0x24),
                BucketIndexMask = (uint)ReadInt32(span, 0x28)
            },
            ValueArray = new NumericDictionaryValueArray
            {
                IsMarkEnabled = isMarkEnabled,
                SubLobType = (SubLobType)ReadInt32(span, 0x2C),
                ElementSize = ReadInt32(span, 0x30),
                ValueCount = ReadInt32(span, 0x34)
            }
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
            Store = new StringDictionaryStore
            {
                IsMarkEnabled = isMarkEnabled,
                SubLobType = (SubLobType)ReadInt32(span, StringDictionaryStore.Offset),
                StringCount = ReadInt32(span, StringDictionaryStore.Offset + 0x04),
                MaxStringSize = ReadInt32(span, StringDictionaryStore.Offset + 0x08),
                Reserved = data.Slice(StringDictionaryStore.Offset + 0x0C,
                                      StringDictionaryStore.Size - 0x0C).ToArray()
            },
            HandleArray = new StringDictionaryArray
            {
                IsMarkEnabled = isMarkEnabled,
                Offset = StringDictionary.HandleArrayHeaderOffset,
                SubLobType = (SubLobType)ReadInt32(span, StringDictionary.HandleArrayHeaderOffset),
                ElementSize = ReadInt32(span, StringDictionary.HandleArrayHeaderOffset + 0x04),
                ElementCount = ReadInt32(span, StringDictionary.HandleArrayHeaderOffset + 0x08)
            },
            PageSizeArray = new StringDictionaryArray
            {
                IsMarkEnabled = isMarkEnabled,
                Offset = StringDictionary.PageSizeArrayHeaderOffset,
                SubLobType = (SubLobType)ReadInt32(span, StringDictionary.PageSizeArrayHeaderOffset),
                ElementSize = ReadInt32(span, StringDictionary.PageSizeArrayHeaderOffset + 0x04),
                ElementCount = ReadInt32(span, StringDictionary.PageSizeArrayHeaderOffset + 0x08)
            }
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
        dictionary.MarkProperty(nameof(StringDictionary.Store),
                                StringDictionaryStore.Offset,
                                StringDictionaryStore.Size);

        dictionary.MarkProperty(nameof(StringDictionary.HandleArray),
                                StringDictionary.HandleArrayHeaderOffset,
                                StringDictionaryArray.Size);

        dictionary.MarkProperty(nameof(StringDictionary.PageSizeArray),
                                StringDictionary.PageSizeArrayHeaderOffset,
                                StringDictionaryArray.Size);

        dictionary.Store.Mark();

        dictionary.HandleArray.Mark();

        dictionary.PageSizeArray.Mark();
    }

    private static void MarkNumericHeader(NumericDictionary dictionary)
    {
        dictionary.MarkProperty(nameof(NumericDictionary.Version), 0x00, 4);
        dictionary.MarkProperty(nameof(NumericDictionary.LobType), 0x04, 4);
        dictionary.MarkProperty(nameof(NumericDictionary.Reserved), 0x08, 4);
        dictionary.MarkProperty(nameof(NumericDictionary.HashTable),
                                NumericDictionaryHashTable.Offset,
                                NumericDictionaryHashTable.Size);

        dictionary.MarkProperty(nameof(NumericDictionary.ValueArray),
                                NumericDictionaryValueArray.Offset,
                                NumericDictionaryValueArray.Size);

        dictionary.HashTable.Mark();

        dictionary.ValueArray.Mark();
    }

    private static int ReadInt32(ReadOnlySpan<byte> span, int offset)
        => BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));
}
