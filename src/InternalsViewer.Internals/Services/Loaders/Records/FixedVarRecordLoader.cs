using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;
using InternalsViewer.Internals.Services.Loaders.Records.Fields;
using System.Collections;

namespace InternalsViewer.Internals.Services.Loaders.Records;

/// <summary>
/// Loads a record in the FixedVar format
/// </summary>
public abstract class FixedVarRecordLoader
{
    /// <summary>
    /// Gets the variable offset array.
    /// </summary>
    /// <returns>An array of 2-byte integers representing a start offset in the page</returns>
    protected static ushort[] GetOffsetArray(byte[] record, int size, int offset)
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
    protected static void LoadStatusBitsA(FixedVarRecord record, byte[] data)
    {
        var statusA = data[record.SlotOffset];

        record.StatusBitsA = new BitArray(new[] { statusA });

        record.MarkProperty("StatusBitsADescription", record.SlotOffset, 1);

        record.RecordType = (RecordType)(statusA >> 1 & 7);

        record.HasNullBitmap = record.StatusBitsA[4];
        record.HasVariableLengthColumns = record.StatusBitsA[5];
        record.HasRowVersioning = record.StatusBitsA[6];
    }

    /// <summary>
    /// Loads a LOB field.
    /// </summary>
    protected static void LoadLobField(FixedVarRecordField field, byte[] data, int offset)
    {
        field.MarkProperty("BlobInlineRoot");

        // First byte gives the Blob field type
        switch ((BlobFieldType)data[0])
        {
            case BlobFieldType.LobPointer:
                field.BlobInlineRoot = LobPointerFieldLoader.Load(data, offset);
                break;

            case BlobFieldType.LobRoot:
                field.BlobInlineRoot = LobRootFieldLoader.Load(data, offset);
                break;

            case BlobFieldType.RowOverflow:
                field.BlobInlineRoot = LobOverflowFieldLoader.Load(data, offset);
                break;
        }
    }
}