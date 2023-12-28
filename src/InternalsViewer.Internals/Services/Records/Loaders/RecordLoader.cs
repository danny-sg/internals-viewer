using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;
using System.Collections;

namespace InternalsViewer.Internals.Services.Records.Loaders;

/// <summary>
/// Loads a record
/// </summary>
public abstract class RecordLoader
{
    /// <summary>
    /// Gets the variable offset array.
    /// </summary>
    /// <returns>An array of 2-byte integers representing a start offset in the page</returns>
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
    /// Loads Status Bits A, part of the two byte record header
    /// </summary>
    protected static void LoadStatusBitsA(Record record, byte[] data)
    {
        var statusA = data[record.SlotOffset];

        record.StatusBitsA = new BitArray(new[] { statusA });

        record.MarkDataStructure("StatusBitsADescription", record.SlotOffset, 1);

        record.RecordType = (RecordType)(statusA >> 1 & 7);

        record.HasNullBitmap = record.StatusBitsA[4];
        record.HasVariableLengthColumns = record.StatusBitsA[5];
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