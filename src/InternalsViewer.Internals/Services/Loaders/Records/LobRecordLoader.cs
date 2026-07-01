using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Blob;
using InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;

namespace InternalsViewer.Internals.Services.Loaders.Records;

public class LobRecordLoader : FixedVarRecordLoader
{
    /// <summary>
    /// Loads a record at the specified offset
    /// </summary>
    internal static LobRecord Load(LobPage page, ushort offset, bool isMarkEnabled = false)
    {
        var record = new LobRecord
        {
            IsMarkEnabled = isMarkEnabled
        };

        record.Offset = offset;

        var statusByte = page.Data[record.Offset];

        var data = page.Data;

        LoadStatusBitsA(record, data);

        record.StatusBitsB = data[record.Offset + sizeof(byte)];

        record.MarkProperty(nameof(LobRecord.StatusBitsB), record.Offset + sizeof(byte), sizeof(byte));

        record.RecordType = (RecordType)((statusByte >> 1) & 7);

        record.MarkProperty(nameof(LobRecord.Length), record.Offset + LobRecord.LengthOffset, sizeof(short));

        record.Length = BitConverter.ToInt16(data, record.Offset + LobRecord.LengthOffset);

        record.MarkProperty(nameof(LobRecord.BlobId), record.Offset + LobRecord.IdOffset, sizeof(long));

        record.BlobId = BitConverter.ToInt64(data, record.Offset + LobRecord.IdOffset);

        record.MarkProperty(nameof(LobRecord.BlobType), record.Offset + LobRecord.TypeOffset, sizeof(short));

        record.BlobType = (BlobType)data[record.Offset + LobRecord.TypeOffset];

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

        return record;
    }

    private static void LoadLargeRoot(LobRecord record, byte[] data)
    {
        record.BlobChildren = new List<BlobChildLink>();

        record.MarkProperty(nameof(LobRecord.MaxLinks), record.Offset + LobRecord.MaxLinksOffset, sizeof(short));

        record.MaxLinks = BitConverter.ToInt16(data, record.Offset + LobRecord.MaxLinksOffset);

        record.MarkProperty(nameof(LobRecord.CurLinks), record.Offset + LobRecord.CurLinksOffset, sizeof(short));

        record.CurLinks = BitConverter.ToInt16(data, record.Offset + LobRecord.CurLinksOffset);

        record.MarkProperty(nameof(LobRecord.Level), record.Offset + LobRecord.RootLevelOffset, sizeof(short));

        record.Level = BitConverter.ToInt16(data, record.Offset + LobRecord.RootLevelOffset);

        for (var i = 0; i < record.CurLinks; i++)
        {
            if (record.BlobType == BlobType.LargeRoot)
            {
                var (link, offset, length) = LoadRootBlobChild(record, i, data);

                record.MarkValue(ItemType.BlobChildOffset, $"Child {i}", link, offset, length);

                record.BlobChildren.Add(link);
            }
            else
            {
                var (link, offset, length) = LoadInternalBlobChild(record, i, data);

                record.MarkValue(ItemType.BlobChildOffset, $"Child {i}", link, offset, length);

                record.BlobChildren.Add(link);
            }
        }
    }

    private static void LoadSmallRoot(LobRecord record, byte[] data)
    {
        record.MarkProperty(nameof(LobRecord.Size), record.Offset + LobRecord.MaxLinksOffset, sizeof(short));

        record.Size = BitConverter.ToInt16(data, record.Offset + LobRecord.MaxLinksOffset);

        record.Data = new byte[record.Size];

        record.MarkProperty(nameof(LobRecord.Data), record.Offset + LobRecord.SmallDataOffset, record.Size);

        Array.Copy(data,
                   record.Offset + LobRecord.SmallDataOffset,
                   record.Data,
                   0,
                   record.Size);
    }

    private static void LoadData(LobRecord lobRecord, byte[] data)
    {
        lobRecord.MarkProperty(nameof(LobRecord.Data), lobRecord.Offset + LobRecord.DataOffset, lobRecord.Length);

        lobRecord.Data = new byte[lobRecord.Length];

        Array.Copy(data,
                   lobRecord.Offset + LobRecord.DataOffset,
                   lobRecord.Data,
                   0,
                   lobRecord.Length);
    }

    private static (BlobChildLink Link, int Offset, int Length) LoadInternalBlobChild(LobRecord lobRecord, int index, byte[] data)
    {
        var blobChildLink = new BlobChildLink();

        var offsetPosition = lobRecord.Offset + LobRecord.InternalChildOffset + (index * 16);

        var offset = BitConverter.ToInt32(data, offsetPosition);

        blobChildLink.Offset = offset;

        blobChildLink.MarkProperty(nameof(BlobChildLink.Offset), offsetPosition, sizeof(int));

        var rowId = new RowIdentifier(data.AsSpan(offsetPosition + 8, 8));

        blobChildLink.RowIdentifier = rowId;

        blobChildLink.MarkValue(ItemType.Rid, "At", rowId, offsetPosition + 8, 8);

        return (blobChildLink, offsetPosition, 16);
    }

    private static (BlobChildLink Link, int Offset, int Length) LoadRootBlobChild(LobRecord record, int index, byte[] data)
    {
        var blobChildLink = new BlobChildLink();

        var offsetPosition = record.Offset + LobRecord.RootChildOffset + (index * 12);

        blobChildLink.MarkProperty(nameof(BlobChildLink.Offset), offsetPosition, sizeof(int));

        var offset = BitConverter.ToInt32(data, offsetPosition);

        var rowIdPosition = record.Offset + LobRecord.RootChildOffset + (index * 12) + 4;

        var rowId = new RowIdentifier(data.AsSpan(rowIdPosition, 8));

        blobChildLink.MarkValue(ItemType.Rid, "At", rowId, rowIdPosition, 8);

        blobChildLink.RowIdentifier = rowId;
        blobChildLink.Offset = offset;

        return (blobChildLink, offsetPosition, sizeof(int) + 8);
    }
}