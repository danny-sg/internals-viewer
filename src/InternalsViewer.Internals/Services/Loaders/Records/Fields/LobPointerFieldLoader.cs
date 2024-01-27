using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;

namespace InternalsViewer.Internals.Services.Loaders.Records.Fields;

public class LobPointerFieldLoader
{
    public const int RowIdOffset = 8;

    public static PointerField Load(byte[] data, int offset)
    {
        var field = new PointerField();

        field.MarkProperty("PointerType", offset, sizeof(byte));

        field.PointerType = (BlobFieldType)data[0];

        field.MarkProperty("Timestamp", offset + sizeof(byte), sizeof(int));

        field.Timestamp = BitConverter.ToUInt32(data, 0);

        LoadLinks(field, data, offset);

        return field;
    }

    private static void LoadLinks(PointerField field, byte[] data, int offset)
    {
        field.Links = new List<BlobChildLink>();

        var rowIdData = new byte[8];
        Array.Copy(data, RowIdOffset, rowIdData, 0, 8);

        field.MarkProperty("LinksArray", string.Empty, 0);

        var rowId = new RowIdentifier(rowIdData);

        var link = new BlobChildLink(rowId, 0, 0);

        link.MarkProperty("RowIdentifier", offset + RowIdOffset, 8);

        field.Links.Add(link);
    }
}