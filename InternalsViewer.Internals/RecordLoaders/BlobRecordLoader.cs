using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Records;
using InternalsViewer.Internals.BlobPointers;
using System.Collections;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Blob;

namespace InternalsViewer.Internals.RecordLoaders;

internal class BlobRecordLoader : RecordLoader
{
    /// <summary>
    /// Loads the specified record.
    /// </summary>
    internal static void Load(BlobRecord record)
    {
        var statusByte = record.Page.PageData[record.SlotOffset];

        record.Mark("StatusBitsADescription", record.SlotOffset, sizeof(byte));

        record.StatusBitsA = new BitArray(new[] { statusByte });

        record.Mark("StatusBitsBDescription", record.SlotOffset + sizeof(byte), sizeof(byte));

        record.RecordType = (RecordType)((statusByte >> 1) & 7);

        record.Mark("Length", record.SlotOffset + BlobRecord.LengthOffset, sizeof(short));

        record.Length = BitConverter.ToInt16(record.Page.PageData, record.SlotOffset + BlobRecord.LengthOffset);

        record.Mark("BlobId", record.SlotOffset + BlobRecord.IdOffset, sizeof(long));

        record.BlobId = BitConverter.ToInt64(record.Page.PageData, record.SlotOffset + BlobRecord.IdOffset);

        record.Mark("BlobTypeDescription", record.SlotOffset + BlobRecord.TypeOffset, sizeof(short));

        record.BlobType = (BlobType)record.Page.PageData[record.SlotOffset + BlobRecord.TypeOffset];

        switch (record.BlobType)
        {
            case BlobType.LargeRoot:
            case BlobType.Internal:

                LoadLargeRoot(record);
                break;

            case BlobType.SmallRoot:

                LoadSmallRoot(record);
                break;

            case BlobType.Data:

                LoadData(record);
                break;
        }
    }

    private static void LoadLargeRoot(BlobRecord record)
    {
        record.BlobChildren = new List<BlobChildLink>();

        record.Mark("MaxLinks", record.SlotOffset + BlobRecord.MaxLinksOffset, sizeof(short));

        record.MaxLinks = BitConverter.ToInt16(record.Page.PageData, record.SlotOffset + BlobRecord.MaxLinksOffset);

        record.Mark("CurLinks", record.SlotOffset + BlobRecord.CurLinksOffset, sizeof(short));

        record.CurLinks = BitConverter.ToInt16(record.Page.PageData, record.SlotOffset + BlobRecord.CurLinksOffset);

        record.Mark("Level", record.SlotOffset + BlobRecord.RootLevelOffset, sizeof(short));

        record.Level = BitConverter.ToInt16(record.Page.PageData, record.SlotOffset + BlobRecord.RootLevelOffset);

        for (var i = 0; i < record.CurLinks; i++)
        {
            record.Mark("BlobChildrenArray", "Child " + i + " ", i);

            BlobChildLink link;

            if (record.BlobType == BlobType.LargeRoot)
            {
                link = LoadRootBlobChild(record, i);
            }
            else
            {
                link = LoadInternalBlobChild(record, i);
            }

            record.BlobChildren.Add(link);
        }
    }

    private static void LoadSmallRoot(BlobRecord record)
    {
        record.Mark("Size", record.SlotOffset + BlobRecord.MaxLinksOffset, sizeof(short));

        record.Size = BitConverter.ToInt16(record.Page.PageData, record.SlotOffset + BlobRecord.MaxLinksOffset);

        record.Data = new byte[record.Size];

        record.Mark("Data", record.SlotOffset + BlobRecord.SmallDataOffset, record.Size);

        Array.Copy(record.Page.PageData,
            record.SlotOffset + BlobRecord.SmallDataOffset,
            record.Data,
            0,
            record.Size);
    }

    private static void LoadData(BlobRecord blobRecord)
    {
        blobRecord.Mark("Data", blobRecord.SlotOffset + BlobRecord.DataOffset, blobRecord.Length);

        blobRecord.Data = new byte[blobRecord.Length];

        Array.Copy(blobRecord.Page.PageData,
            blobRecord.SlotOffset + BlobRecord.DataOffset,
            blobRecord.Data,
            0,
            blobRecord.Length);
    }

    private static BlobChildLink LoadInternalBlobChild(BlobRecord blobRecord, int index)
    {
        var offset = BitConverter.ToInt32(blobRecord.Page.PageData,
            blobRecord.SlotOffset + BlobRecord.InternalChildOffset + (index * 16));

        var rowData = new byte[8];

        Array.Copy(blobRecord.Page.PageData,
            blobRecord.SlotOffset + BlobRecord.InternalChildOffset + (index * 16) + 8,
            rowData,
            0,
            8);

        var rowId = new RowIdentifier(rowData);

        return new BlobChildLink(rowId, offset, offset);
    }

    private static BlobChildLink LoadRootBlobChild(BlobRecord record, int index)
    {
        var blobChildLink = new BlobChildLink();

        var offsetPosition =  record.SlotOffset + BlobRecord.RootChildOffset + (index * 12);

        blobChildLink.Mark("Offset", offsetPosition, sizeof(int));

        var offset = BitConverter.ToInt32(record.Page.PageData, offsetPosition);

        var rowData = new byte[8];

        var rowIdPosition = record.SlotOffset + BlobRecord.RootChildOffset + (index * 12) + 4;

        blobChildLink.Mark("RowIdentifier", rowIdPosition, 8);

        Array.Copy(record.Page.PageData, rowIdPosition, rowData, 0, 8);

        var rowId = new RowIdentifier(rowData);

        blobChildLink.RowIdentifier = rowId;
        blobChildLink.Offset = offset;

        return blobChildLink;
    }
}