using InternalsViewer.Internals.Engine;
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
    private const byte HeaderVersionOffset = 0;
    private const byte PageTypeOffset = 1;
    private const byte TypeFlagBitsOffset = 2;
    private const byte LevelOffset = 3;
    private const byte FlagBitsOffset = 4;
    private const byte IndexIdOffset = 6;
    private const byte PreviousPageOffset = 8;
    private const byte FixedLengthOffset = 14;
    private const byte NextPageOffset = 16;
    private const byte SlotCountOffset = 22;
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

    public static PageHeader Parse(byte[] data)
    {
        var header = new PageHeader();

        ReadValues(data, header);

        SetHeaderMarkers(header);

        return header;
    }

    private static void ReadValues(byte[] data, PageHeader pageHeader)
    {
        pageHeader.PageType = (PageType)data[PageTypeOffset];

        pageHeader.PageAddress = PageAddressParser.Parse(data, PageAddressOffset);

        pageHeader.PreviousPage = PageAddressParser.Parse(data, PreviousPageOffset);
        pageHeader.NextPage = PageAddressParser.Parse(data, NextPageOffset);

        pageHeader.InternalObjectId = BitConverter.ToInt32(data, ObjectIdOffset);
        pageHeader.InternalIndexId = BitConverter.ToInt16(data, IndexIdOffset);

        pageHeader.Level = data[LevelOffset];

        pageHeader.HeaderVersion = data[HeaderVersionOffset];

        pageHeader.TypeFlagBits = data[TypeFlagBitsOffset];
        pageHeader.FlagBits = BitConverter.ToInt16(data, FlagBitsOffset);

        pageHeader.FixedLengthSize = BitConverter.ToInt16(data, FixedLengthOffset);

        pageHeader.SlotCount = BitConverter.ToInt16(data, SlotCountOffset);
        pageHeader.FreeCount = BitConverter.ToInt16(data, FreeCountOffset);
        pageHeader.ReservedCount = BitConverter.ToInt16(data, ReservedCountOffset);

        pageHeader.TransactionReservedCount = BitConverter.ToInt16(data, TransactionReservedCountOffset);

        pageHeader.InternalTransactionId = PageAddressParser.Parse(data, InternalTransactionIdOffset);
        pageHeader.GhostRecordCount = BitConverter.ToInt16(data, GhostRecordCountOffset);
        pageHeader.FreeData = BitConverter.ToInt16(data, FreeDataOffset);

        pageHeader.TornBits = BitConverter.ToInt32(data, TornBitsOffset);

        pageHeader.Lsn = LogSequenceNumberParser.Parse(data, LsnOffset);
    }

    private static void SetHeaderMarkers(DataStructure header)
    {
        header.MarkProperty(nameof(PageHeader.HeaderVersion), HeaderVersionOffset, sizeof(byte));

        header.MarkProperty(nameof(PageHeader.PageType), PageTypeOffset, sizeof(byte));

        header.MarkProperty(nameof(PageHeader.TypeFlagBits), TypeFlagBitsOffset, sizeof(byte));

        header.MarkProperty(nameof(PageHeader.Level), LevelOffset, sizeof(byte));

        header.MarkProperty(nameof(PageHeader.FlagBits), FlagBitsOffset, sizeof(short));

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
}
