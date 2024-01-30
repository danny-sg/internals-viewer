using System.Collections;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Blob;
using InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.Services.Loaders.Records;

internal class BlobFixedVarRecordLoader : FixedVarRecordLoader
{
    /// <summary>
    /// Loads a record at the specified offset
    /// </summary>
    internal static void Load(BlobRecord record, Page page, ushort offset)
    {
        record.SlotOffset = offset;

        var statusByte = page.Data[record.SlotOffset];

        var data = page.Data;

        record.MarkProperty(nameof(BlobRecord.StatusBitsA), record.SlotOffset, sizeof(byte));

        record.StatusBitsA = statusByte;

        record.MarkProperty(nameof(BlobRecord.StatusBitsB), record.SlotOffset + sizeof(byte), sizeof(byte));

        record.RecordType = (RecordType)(statusByte >> 1 & 7);

        record.MarkProperty(nameof(BlobRecord.Length), record.SlotOffset + BlobRecord.LengthOffset, sizeof(short));

        record.Length = BitConverter.ToInt16(data, record.SlotOffset + BlobRecord.LengthOffset);

        record.MarkProperty(nameof(BlobRecord.BlobId), record.SlotOffset + BlobRecord.IdOffset, sizeof(long));

        record.BlobId = BitConverter.ToInt64(data, record.SlotOffset + BlobRecord.IdOffset);

        record.MarkProperty(nameof(BlobRecord.BlobType), record.SlotOffset + BlobRecord.TypeOffset, sizeof(short));

        record.BlobType = (BlobType)data[record.SlotOffset + BlobRecord.TypeOffset];

        switch (record.BlobType)
        {
            case BlobType.LargeRoot:
            case BlobType.Internal:

                LoadLargeRoot(record, data);
                break;

            case BlobType.SmallRoot:

                LoadSmallRoot(record, data);
                break;

            case BlobType.Data:

                LoadData(record, data);
                break;
        }
    }

    private static void LoadLargeRoot(BlobRecord record, byte[] data)
    {
        record.BlobChildren = new List<BlobChildLink>();

        record.MarkProperty(nameof(BlobRecord.MaxLinks), record.SlotOffset + BlobRecord.MaxLinksOffset, sizeof(short));

        record.MaxLinks = BitConverter.ToInt16(data, record.SlotOffset + BlobRecord.MaxLinksOffset);

        record.MarkProperty(nameof(BlobRecord.CurLinks), record.SlotOffset + BlobRecord.CurLinksOffset, sizeof(short));

        record.CurLinks = BitConverter.ToInt16(data, record.SlotOffset + BlobRecord.CurLinksOffset);

        record.MarkProperty(nameof(BlobRecord.Level), record.SlotOffset + BlobRecord.RootLevelOffset, sizeof(short));

        record.Level = BitConverter.ToInt16(data, record.SlotOffset + BlobRecord.RootLevelOffset);

        for (var i = 0; i < record.CurLinks; i++)
        {
            record.MarkProperty(nameof(BlobRecord.BlobChildrenArray), "Child " + i + " ", i);

            BlobChildLink link;

            if (record.BlobType == BlobType.LargeRoot)
            {
                link = LoadRootBlobChild(record, i, data);
            }
            else
            {
                link = LoadInternalBlobChild(record, i, data);
            }

            record.BlobChildren.Add(link);
        }
    }

    private static void LoadSmallRoot(BlobRecord record, byte[] data)
    {
        record.MarkProperty(nameof(BlobRecord.Size), record.SlotOffset + BlobRecord.MaxLinksOffset, sizeof(short));

        record.Size = BitConverter.ToInt16(data, record.SlotOffset + BlobRecord.MaxLinksOffset);

        record.Data = new byte[record.Size];

        record.MarkProperty(nameof(BlobRecord.Data), record.SlotOffset + BlobRecord.SmallDataOffset, record.Size);

        Array.Copy(data,
                   record.SlotOffset + BlobRecord.SmallDataOffset,
                   record.Data,
                   0,
                   record.Size);
    }

    private static void LoadData(BlobRecord blobRecord, byte[] data)
    {
        blobRecord.MarkProperty(nameof(BlobRecord.Data), blobRecord.SlotOffset + BlobRecord.DataOffset, blobRecord.Length);

        blobRecord.Data = new byte[blobRecord.Length];

        Array.Copy(data,
                   blobRecord.SlotOffset + BlobRecord.DataOffset,
                   blobRecord.Data,
                   0,
                   blobRecord.Length);
    }

    private static BlobChildLink LoadInternalBlobChild(BlobRecord blobRecord, int index, byte[] data)
    {
        var offset = BitConverter.ToInt32(data,
            blobRecord.SlotOffset + BlobRecord.InternalChildOffset + index * 16);

        var rowData = new byte[8];

        Array.Copy(data,
            blobRecord.SlotOffset + BlobRecord.InternalChildOffset + index * 16 + 8,
            rowData,
            0,
            8);

        var rowId = new RowIdentifier(rowData);

        return new BlobChildLink(rowId, offset, offset);
    }

    private static BlobChildLink LoadRootBlobChild(BlobRecord record, int index, byte[] data)
    {
        var blobChildLink = new BlobChildLink();

        var offsetPosition = record.SlotOffset + BlobRecord.RootChildOffset + index * 12;

        blobChildLink.MarkProperty(nameof(BlobChildLink.Offset), offsetPosition, sizeof(int));

        var offset = BitConverter.ToInt32(data, offsetPosition);

        var rowData = new byte[8];

        var rowIdPosition = record.SlotOffset + BlobRecord.RootChildOffset + index * 12 + 4;

        blobChildLink.MarkProperty(nameof(BlobChildLink.RowIdentifier), rowIdPosition, 8);

        Array.Copy(data, rowIdPosition, rowData, 0, 8);

        var rowId = new RowIdentifier(rowData);

        blobChildLink.RowIdentifier = rowId;
        blobChildLink.Offset = offset;

        return blobChildLink;
    }
}