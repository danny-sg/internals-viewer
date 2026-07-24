using System.Buffers.Binary;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Parsers;

namespace InternalsViewer.Internals.Services.Pages.Parsers;

/// <summary>
/// Responsible for loading the header of a Page
/// </summary>
public static class PageHeaderParser
{    
    internal const byte PreviousPageOffset = 8;
    internal const byte PageTypeOffset = 1;
    internal const byte LevelOffset = 3;
    internal const byte FixedLengthOffset = 14;
    internal const byte NextPageOffset = 16;
    internal const byte SlotCountOffset = 22;

    private const byte HeaderVersionOffset = 0;
    private const byte TypeFlagBitsOffset = 2;
    private const byte FlagBitsOffset = 4;
    private const byte IndexIdOffset = 6;
    private const byte ObjectIdOffset = 24;
    private const byte FreeCountOffset = 28;
    private const byte FreeDataOffset = 30;
    private const byte PageAddressOffset = 32;
    private const byte ReservedCountOffset = 38;
    private const byte LsnOffset = 40;
    private const byte TransactionReservedCountOffset = 50;
    private const byte InternalTransactionIdOffset = 52;
    private const byte GhostRecordCountOffset = 58;
    private const byte TornBitsOffset = 60;
    private const byte UnusedOffset = 62;

    private static readonly (int Bit, string Name)[] FlagBitsNames =
    [
        (0x2, "PG_ALIGNED4"),
        (0x4, "Fixed Length Row"),
        (0x8, "Has Free Slot"),
        (0x200, "Has Checksum"),
        (0x2000, "Version Info"),
        (0x4000, "ADD_BEG"),
        (0x8000, "ADD_END"),
    ];

    public static PageHeader Parse(byte[] data, bool isMarkEnabled = true)
    {
        return Parse(data.AsSpan(), isMarkEnabled);
    }

    public static PageHeader Parse(ReadOnlySpan<byte> data, bool isMarkEnabled = true)
    {
        var header = new PageHeader
        {
            IsMarkEnabled = isMarkEnabled
        };

        ReadValues(data, header);

        if (isMarkEnabled)
        {
            SetHeaderMarkers(header);
        }

        return header;
    }

    private static void ReadValues(ReadOnlySpan<byte> data, PageHeader pageHeader)
    {
        pageHeader.PageType = (PageType)data[PageTypeOffset];

        pageHeader.PageAddress = PageAddressParser.Parse(data, PageAddressOffset);

        pageHeader.PreviousPage = PageAddressParser.Parse(data, PreviousPageOffset);
        pageHeader.NextPage = PageAddressParser.Parse(data, NextPageOffset);

        pageHeader.InternalObjectId = BinaryPrimitives.ReadInt32LittleEndian(data[ObjectIdOffset..]);
        pageHeader.InternalIndexId = BinaryPrimitives.ReadInt16LittleEndian(data[IndexIdOffset..]);

        pageHeader.Level = data[LevelOffset];

        pageHeader.HeaderVersion = data[HeaderVersionOffset];

        pageHeader.TypeFlagBits = data[TypeFlagBitsOffset];
        pageHeader.FlagBits = BinaryPrimitives.ReadInt16LittleEndian(data[FlagBitsOffset..]);

        pageHeader.FixedLengthSize = BinaryPrimitives.ReadUInt16LittleEndian(data[FixedLengthOffset..]);

        pageHeader.SlotCount = BinaryPrimitives.ReadUInt16LittleEndian(data[SlotCountOffset..]);
        pageHeader.FreeCount = BinaryPrimitives.ReadUInt16LittleEndian(data[FreeCountOffset..]);
        pageHeader.ReservedCount = BinaryPrimitives.ReadUInt16LittleEndian(data[ReservedCountOffset..]);

        pageHeader.TransactionReservedCount = BinaryPrimitives.ReadInt16LittleEndian(data[TransactionReservedCountOffset..]);

        pageHeader.InternalTransactionId = PageAddressParser.Parse(data, InternalTransactionIdOffset);
        pageHeader.GhostRecordCount = BinaryPrimitives.ReadInt16LittleEndian(data[GhostRecordCountOffset..]);
        pageHeader.FreeData = BinaryPrimitives.ReadUInt16LittleEndian(data[FreeDataOffset..]);

        pageHeader.TornBits = BinaryPrimitives.ReadInt32LittleEndian(data[TornBitsOffset..]);

        pageHeader.Lsn = LogSequenceNumberParser.Parse(data[LsnOffset..]);
    }

    private static void SetHeaderMarkers(PageHeader header)
    {
        header.MarkProperty(nameof(PageHeader.HeaderVersion), HeaderVersionOffset, sizeof(byte));

        header.MarkProperty(nameof(PageHeader.PageType), PageTypeOffset, sizeof(byte));

        header.MarkProperty(nameof(PageHeader.TypeFlagBits),
                            TypeFlagBitsOffset,
                            sizeof(byte),
                            GetTypeFlagBitsTags(header.TypeFlagBits, header.PageType));

        header.MarkProperty(nameof(PageHeader.Level), LevelOffset, sizeof(byte));

        header.MarkProperty(nameof(PageHeader.FlagBits), FlagBitsOffset, sizeof(short), GetFlagBitsTags(header.FlagBits));

        header.MarkProperty(nameof(PageHeader.InternalIndexId), IndexIdOffset, sizeof(short));

        header.MarkProperty(nameof(PageHeader.PreviousPage), PreviousPageOffset, PageAddress.Size);

        header.MarkProperty(nameof(PageHeader.FixedLengthSize), FixedLengthOffset, sizeof(short));

        header.MarkProperty(nameof(PageHeader.NextPage), NextPageOffset, PageAddress.Size);

        header.MarkProperty(nameof(PageHeader.SlotCount), SlotCountOffset, sizeof(short));

        header.MarkProperty(nameof(PageHeader.InternalObjectId), ObjectIdOffset, sizeof(int));

        header.MarkProperty(nameof(PageHeader.FreeCount), FreeCountOffset, sizeof(short));

        header.MarkProperty(nameof(PageHeader.FreeData), FreeDataOffset, sizeof(short));

        header.MarkProperty(nameof(PageHeader.PageAddress), PageAddressOffset, PageAddress.Size);

        header.MarkProperty(nameof(PageHeader.ReservedCount), ReservedCountOffset, sizeof(short));

        header.MarkProperty(nameof(PageHeader.Lsn), LsnOffset, LogSequenceNumber.Size);

        header.MarkProperty(nameof(PageHeader.TransactionReservedCount), TransactionReservedCountOffset, sizeof(short));

        header.MarkProperty(nameof(PageHeader.InternalTransactionId), InternalTransactionIdOffset, PageAddress.Size);

        header.MarkProperty(nameof(PageHeader.GhostRecordCount), GhostRecordCountOffset, sizeof(short));

        header.MarkProperty(nameof(PageHeader.TornBits), TornBitsOffset, sizeof(int));

        header.MarkProperty(nameof(PageHeader.AllocationUnitId));
    }

    private static string[] GetFlagBitsTags(short flagBits)
    {
        var tags = new List<string>();

        foreach (var (bit, name) in FlagBitsNames)
        {
            if ((flagBits & bit) != 0)
            {
                tags.Add(name);
            }
        }

        return [.. tags];
    }

    private static string[] GetTypeFlagBitsTags(byte typeFlagBits, PageType pageType)
    {
        var tags = new List<string>();

        if (pageType == PageType.Pfs)
        {
            if ((typeFlagBits & 0x1) != 0)
            {
                tags.Add("Has Ghosts");
            }

            if ((typeFlagBits & 0x4) != 0)
            {
                tags.Add("Has Version Pages");
            }
        }

        return [.. tags];
    }
}
