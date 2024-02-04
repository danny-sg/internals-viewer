using System.Collections;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Blob;
using InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;
using InternalsViewer.Internals.Records;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;

namespace InternalsViewer.Internals.Services.Loaders.Records;

internal class BlobFixedVarRecordLoader : FixedVarRecordLoader
{
    /// <summary>
    /// Loads a record at the specified offset
    /// </summary>
    internal static void Load(BlobRecord record, Page page, ushort offset)
    {
        record.Offset = offset;

        var statusByte = page.Data[record.Offset];

        var data = page.Data;

        record.MarkProperty(nameof(BlobRecord.StatusBitsA), record.Offset, sizeof(byte));

        record.StatusBitsA = statusByte;

        record.MarkProperty(nameof(BlobRecord.StatusBitsB), record.Offset + sizeof(byte), sizeof(byte));

        record.RecordType = (RecordType)(statusByte >> 1 & 7);

        record.MarkProperty(nameof(BlobRecord.Length), record.Offset + BlobRecord.LengthOffset, sizeof(short));

        record.Length = BitConverter.ToInt16(data, record.Offset + BlobRecord.LengthOffset);

        record.MarkProperty(nameof(BlobRecord.BlobId), record.Offset + BlobRecord.IdOffset, sizeof(long));

        record.BlobId = BitConverter.ToInt64(data, record.Offset + BlobRecord.IdOffset);

        record.MarkProperty(nameof(BlobRecord.BlobType), record.Offset + BlobRecord.TypeOffset, sizeof(short));

        record.BlobType = (BlobType)data[record.Offset + BlobRecord.TypeOffset];

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

        record.MarkProperty(nameof(BlobRecord.MaxLinks), record.Offset + BlobRecord.MaxLinksOffset, sizeof(short));

        record.MaxLinks = BitConverter.ToInt16(data, record.Offset + BlobRecord.MaxLinksOffset);

        record.MarkProperty(nameof(BlobRecord.CurLinks), record.Offset + BlobRecord.CurLinksOffset, sizeof(short));

        record.CurLinks = BitConverter.ToInt16(data, record.Offset + BlobRecord.CurLinksOffset);

        record.MarkProperty(nameof(BlobRecord.Level), record.Offset + BlobRecord.RootLevelOffset, sizeof(short));

        record.Level = BitConverter.ToInt16(data, record.Offset + BlobRecord.RootLevelOffset);

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
        record.MarkProperty(nameof(BlobRecord.Size), record.Offset + BlobRecord.MaxLinksOffset, sizeof(short));

        record.Size = BitConverter.ToInt16(data, record.Offset + BlobRecord.MaxLinksOffset);

        record.Data = new byte[record.Size];

        record.MarkProperty(nameof(BlobRecord.Data), record.Offset + BlobRecord.SmallDataOffset, record.Size);

        Array.Copy(data,
                   record.Offset + BlobRecord.SmallDataOffset,
                   record.Data,
                   0,
                   record.Size);
    }

    private static void LoadData(BlobRecord blobRecord, byte[] data)
    {
        blobRecord.MarkProperty(nameof(BlobRecord.Data), blobRecord.Offset + BlobRecord.DataOffset, blobRecord.Length);

        blobRecord.Data = new byte[blobRecord.Length];

        Array.Copy(data,
                   blobRecord.Offset + BlobRecord.DataOffset,
                   blobRecord.Data,
                   0,
                   blobRecord.Length);
    }

    private static BlobChildLink LoadInternalBlobChild(BlobRecord blobRecord, int index, byte[] data)
    {
        var offset = BitConverter.ToInt32(data,
            blobRecord.Offset + BlobRecord.InternalChildOffset + index * 16);

        var rowData = new byte[8];

        Array.Copy(data,
            blobRecord.Offset + BlobRecord.InternalChildOffset + index * 16 + 8,
            rowData,
            0,
            8);

        var rowId = new RowIdentifier(rowData);

        return new BlobChildLink(rowId, offset, offset);
    }

    private static BlobChildLink LoadRootBlobChild(BlobRecord record, int index, byte[] data)
    {
        var blobChildLink = new BlobChildLink();

        var offsetPosition = record.Offset + BlobRecord.RootChildOffset + index * 12;

        blobChildLink.MarkProperty(nameof(BlobChildLink.Offset), offsetPosition, sizeof(int));

        var offset = BitConverter.ToInt32(data, offsetPosition);

        var rowData = new byte[8];

        var rowIdPosition = record.Offset + BlobRecord.RootChildOffset + index * 12 + 4;

        blobChildLink.MarkProperty(nameof(BlobChildLink.RowIdentifier), rowIdPosition, 8);

        Array.Copy(data, rowIdPosition, rowData, 0, 8);

        var rowId = new RowIdentifier(rowData);

        blobChildLink.RowIdentifier = rowId;
        blobChildLink.Offset = offset;

        return blobChildLink;
    }
}