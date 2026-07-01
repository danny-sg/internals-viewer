using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.FixedVarRecordType;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Internals.Services.Loaders.Records.Fields;

namespace InternalsViewer.Internals.Services.Loaders.Records.FixedVar;

/// <summary>
/// Loads a record in the FixedVar format
/// </summary>
public abstract class FixedVarRecordLoader
{
    /// <summary>
    /// Loads Status Bits A, part of the two byte record header
    /// </summary>
    protected static void LoadStatusBitsA(FixedVarRecord record, byte[] data)
    {
        var statusA = data[record.Offset];

        record.StatusBitsA = statusA;

        record.RecordType = (RecordType)(statusA >> 1 & 7);

        record.HasNullBitmap = (record.StatusBitsA & 0b10000) != 0;
        record.HasVariableLengthColumns = (record.StatusBitsA & 0b100000) != 0;
        record.HasRowVersioning = (record.StatusBitsA & 0b1000000) != 0;

        var tags = new List<string> { record.RecordType.ToString() };

        tags.AddIf("Has Null Bitmap", record.HasNullBitmap);
        tags.AddIf("Has Variable Length Columns", record.HasVariableLengthColumns);
        tags.AddIf("Has Row Versioning", record.HasRowVersioning);

        record.MarkProperty(nameof(FixedVarRecord.StatusBitsA), record.Offset, 1, tags.ToArray());
    }

    /// <summary>
    /// Loads a LOB field.
    /// </summary>
    protected static void LoadLobField(FixedVarRecordField field, byte[] data, int offset)
    {
        field.MarkProperty(nameof(field.BlobInlineRoot));

        field.BlobInlineRoot = LobFieldLoader.Load(data, offset);
    }
}