using System;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Readers.Headers;

/// <summary>
/// Reads the header of a Page
/// </summary>
public class HeaderReader
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

    public static Header Read(byte[] data)
    {
        var header = new Header();

        SetHeaderValues(data, header);

        SetHeaderMarkers(header);

        return header;
    }

    private static void SetHeaderValues(byte[] data, Header header)
    {
        header.PageType = (PageType)data[PageTypeOffset];

        header.PageAddress = PageAddressParser.Parse(data[PageAddressOffset..(PageAddressOffset + PageAddress.Size)]);

        header.PreviousPage = PageAddressParser.Parse(data[PreviousPageOffset..(PreviousPageOffset + PageAddress.Size)]);
        header.NextPage = PageAddressParser.Parse(data[NextPageOffset..(NextPageOffset + PageAddress.Size)]);

        header.ObjectId = BitConverter.ToInt32(data, ObjectIdOffset);
        header.IndexId = BitConverter.ToInt16(data, IndexIdOffset);

        header.Level = data[LevelOffset];

        header.HeaderVersion = data[HeaderVersionOffset];

        header.TypeFlagBits = data[TypeFlagBitsOffset];
        header.FlagBits = BitConverter.ToInt16(data, FlagBitsOffset);

        header.MinLen = BitConverter.ToInt16(data, MinLenOffset);

        header.SlotCount = BitConverter.ToInt16(data, SlotCountOffset);
        header.FreeCount = BitConverter.ToInt16(data, FreeCountOffset);
        header.ReservedCount = BitConverter.ToInt16(data, ReservedCountOffset);

        header.TransactionReservedCount = BitConverter.ToInt16(data, TransactionReservedCountOffset);
        header.GhostRecordCount = BitConverter.ToInt16(data, GhostRecordCountOffset);
        header.FreeData = BitConverter.ToInt16(data, FreeDataOffset);

        header.TornBits = BitConverter.ToInt32(data, TornBitsOffset);

        header.Lsn = LogSequenceNumberParser.Parse(data[LsnOffset..(LsnOffset + LogSequenceNumber.Size)]);
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
