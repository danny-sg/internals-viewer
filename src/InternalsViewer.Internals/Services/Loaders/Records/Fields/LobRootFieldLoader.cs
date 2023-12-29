using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;

namespace InternalsViewer.Internals.Services.Loaders.Records.Fields;

public class LobRootFieldLoader
{
    public const int ChildOffset = 12;
    public const short LevelOffset = 2;
    public const int TimestampOffset = 6;
    public const int UnusedOffset = 3;
    public const int UpdateSeqOffset = 4;

    public static RootField Load(byte[] data, int offset)
    {
        var field = new RootField();

        field.MarkDataStructure("PointerType", offset, sizeof(byte));

        field.PointerType = (BlobFieldType)data[0];

        field.Unused = data[UnusedOffset];

        field.MarkDataStructure("Unused", offset + UnusedOffset, sizeof(byte));

        field.Level = data[LevelOffset];

        field.MarkDataStructure("Level", offset + LevelOffset, sizeof(byte));

        field.Timestamp = BitConverter.ToInt32(data, TimestampOffset);

        field.MarkDataStructure("Timestamp", offset + TimestampOffset, sizeof(int));

        field.UpdateSeq = BitConverter.ToInt16(data, UpdateSeqOffset);

        field.MarkDataStructure("UpdateSeq", offset + UpdateSeqOffset, sizeof(short));

        LoadLinks(field, data, offset);

        return field;
    }

    private static void LoadLinks(RootField field, byte[] data, int offset)
    {
        field.Links = new List<BlobChildLink>();

        field.SlotCount = (data.Length - 12) / 12;

        for (var i = 0; i < field.SlotCount; i++)
        {
            field.MarkDataStructure("LinksArray", "Child " + i + " - ", i);

            var length = BitConverter.ToInt32(data, ChildOffset + i * 12);

            var rowIdData = new byte[8];
            Array.Copy(data, ChildOffset + i * 12 + 4, rowIdData, 0, 8);

            var rowId = new RowIdentifier(rowIdData);

            var link = new BlobChildLink(rowId, 0, length);

            link.MarkDataStructure("Length", offset + ChildOffset + i * 12, sizeof(int));

            link.MarkDataStructure("RowIdentifier", offset + ChildOffset + i * 12 + sizeof(int), 8);

            field.Links.Add(link);
        }
    }
}