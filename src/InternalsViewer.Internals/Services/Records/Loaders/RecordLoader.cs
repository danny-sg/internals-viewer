using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;

namespace InternalsViewer.Internals.Services.Records.Loaders;

/// <summary>
/// Loads a record
/// </summary>
public abstract class RecordLoader
{
    /// <summary>
    /// Gets the variable offset array.
    /// </summary>
    /// <returns>An array of 2-byte integers</returns>
    public static ushort[] GetOffsetArray(byte[] record, int size, int offset)
    {
        var offsetArray = new ushort[size];

        for (var i = 0; i < size; i++)
        {
            offsetArray[i] = BitConverter.ToUInt16(record, offset);

            offset += sizeof(ushort);
        }

        return offsetArray;
    }

    /// <summary>
    /// Loads a LOB field.
    /// </summary>
    public static void LoadLobField(RecordField field, byte[] data, int offset)
    {
        field.MarkDataStructure("BlobInlineRoot");

        // First byte gives the Blob field type
        switch ((BlobFieldType)data[0])
        {
            case BlobFieldType.LobPointer:
                field.BlobInlineRoot = new PointerField(data, offset);
                break;

            case BlobFieldType.LobRoot:
                field.BlobInlineRoot = new RootField(data, offset);
                break;

            case BlobFieldType.RowOverflow:
                field.BlobInlineRoot = new OverflowField(data, offset);
                break;
        }
    }

    /// <summary>
    /// Flips the high bit if set
    /// </summary>
    public static ushort DecodeOffset(ushort value)
    {
        if ((value | 0x8000) == value)
        {
            return Convert.ToUInt16(value ^ 0x8000);
        }

        return value;
    }
}