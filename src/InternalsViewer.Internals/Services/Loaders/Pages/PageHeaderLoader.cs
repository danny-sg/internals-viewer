using InternalsViewer.Internals.Engine;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Parsers;

namespace InternalsViewer.Internals.Services.Loaders.Pages;

/// <summary>
/// Responsible for loading the header of a Page
/// </summary>
public class PageHeaderLoader
{
    private const byte HeaderVersionOffset = 0;
    private const byte PageTypeOffset = 1;
    private const byte TypeFlagBitsOffset = 2;
    private const byte LevelOffset = 3;
    private const byte FlagBitsOffset = 4;
    private const byte IndexIdOffset = 6;
    private const byte PreviousPageOffset = 8;
    private const byte MinLenOffset = 14;
    private const byte NextPageOffset = 16;
    private const byte SlotCountOffset = 22;
    private const byte ObjectIdOffset = 24;
    private const byte FreeCountOffset = 28;
    private const byte FreeDataOffset = 30;
    private const byte PageAddressOffset = 32;
    private const byte ReservedCountOffset = 38;
    private const byte LsnOffset = 40;
    private const byte TransactionReservedCountOffset = 50;
    private const byte GhostRecordCountOffset = 58;
    private const byte TornBitsOffset = 60;

    public static PageHeader Read(byte[] data)
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

        pageHeader.ObjectId = BitConverter.ToInt32(data, ObjectIdOffset);
        pageHeader.IndexId = BitConverter.ToInt16(data, IndexIdOffset);

        pageHeader.Level = data[LevelOffset];

        pageHeader.HeaderVersion = data[HeaderVersionOffset];

        pageHeader.TypeFlagBits = data[TypeFlagBitsOffset];
        pageHeader.FlagBits = BitConverter.ToInt16(data, FlagBitsOffset);

        pageHeader.MinLen = BitConverter.ToInt16(data, MinLenOffset);

        pageHeader.SlotCount = BitConverter.ToInt16(data, SlotCountOffset);
        pageHeader.FreeCount = BitConverter.ToInt16(data, FreeCountOffset);
        pageHeader.ReservedCount = BitConverter.ToInt16(data, ReservedCountOffset);

        pageHeader.TransactionReservedCount = BitConverter.ToInt16(data, TransactionReservedCountOffset);
        pageHeader.GhostRecordCount = BitConverter.ToInt16(data, GhostRecordCountOffset);
        pageHeader.FreeData = BitConverter.ToInt16(data, FreeDataOffset);

        pageHeader.TornBits = BitConverter.ToInt32(data, TornBitsOffset);

        pageHeader.Lsn = LogSequenceNumberParser.Parse(data, LsnOffset);
    }

    private static void SetHeaderMarkers(DataStructure header)
    {
        header.MarkDataStructure("HeaderVersion", HeaderVersionOffset, sizeof(byte));

        header.MarkDataStructure("PageType", PageTypeOffset, sizeof(byte));

        header.MarkDataStructure("TypeFlagBits", TypeFlagBitsOffset, sizeof(byte));

        header.MarkDataStructure("Level", LevelOffset, sizeof(byte));

        header.MarkDataStructure("FlagBits", FlagBitsOffset, sizeof(short));

        header.MarkDataStructure("IndexId", IndexIdOffset, sizeof(short));

        header.MarkDataStructure("PreviousPage", PreviousPageOffset, PageAddress.Size);

        header.MarkDataStructure("MinLen", MinLenOffset, sizeof(short));

        header.MarkDataStructure("NextPage", NextPageOffset, PageAddress.Size);

        header.MarkDataStructure("SlotCount", SlotCountOffset, sizeof(short));

        header.MarkDataStructure("ObjectId", ObjectIdOffset, sizeof(int));

        header.MarkDataStructure("FreeCount", FreeCountOffset, sizeof(short));

        header.MarkDataStructure("FreeData", FreeDataOffset, sizeof(short));

        header.MarkDataStructure("PageAddress", PageAddressOffset, PageAddress.Size);

        header.MarkDataStructure("ReservedCount", ReservedCountOffset, sizeof(short));

        header.MarkDataStructure("Lsn", LsnOffset, LogSequenceNumber.Size);

        header.MarkDataStructure("TransactionReservedCount", TransactionReservedCountOffset, sizeof(short));

        header.MarkDataStructure("GhostRecordCount", GhostRecordCountOffset, sizeof(short));

        header.MarkDataStructure("TornBits", TornBitsOffset, sizeof(int));
    }
}
