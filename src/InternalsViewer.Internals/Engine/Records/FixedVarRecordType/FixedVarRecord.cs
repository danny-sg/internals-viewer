using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Engine.Records.FixedVarRecordType;

public abstract class FixedVarRecord : Record
{
    [DataStructureItem(ItemType.StatusBitsA)]
    public byte StatusBitsA { get; set; }

    [DataStructureItem(ItemType.StatusBitsB)]
    public byte StatusBitsB { get; set; }

    public short ColumnCountBytes { get; set; }

    public RecordType RecordType { get; set; }

    [DataStructureItem(ItemType.ColumnCountOffset)]
    public short ColumnCountOffset { get; set; }

    public bool HasVariableLengthColumns { get; set; }

    public ushort VariableLengthDataOffset { get; set; }

    [DataStructureItem(ItemType.VariableLengthColumnCount)]
    public ushort VariableLengthColumnCount { get; set; }

    public bool HasNullBitmap { get; set; }

    public short NullBitmapSize { get; set; }

    [DataStructureItem(ItemType.NullBitmap)]
    public byte[] NullBitmap { get; set; } = [];

    public bool HasUniqueifier { get; set; }

    public bool HasRowVersioning { get; set; }

    [DataStructureItem(ItemType.VariableLengthColumnOffsetArray)]
    public ushort[] VariableLengthColumnOffsetArray { get; set; } = [];

    public bool IsNullBitmapSet(ColumnStructure columnStructure, int offset)
    {
        var bitIndex = columnStructure.NullBitIndex - 1 + offset;

        if (columnStructure.NullBitIndex < 1 || bitIndex >= NullBitmap.Length * 8)
        {
            return false;
        }

        return (NullBitmap[bitIndex / 8] & (1 << (bitIndex % 8))) != 0;
    }

    /// <summary>
    /// Gets the record type description.
    /// </summary>
    protected static string GetRecordTypeDescription(RecordType recordType)
    {
        return recordType switch
        {
            RecordType.Primary => "Primary Record",
            RecordType.Forwarded => "Forwarded Record",
            RecordType.ForwardingStub => "Forwarding Stub",
            RecordType.Index => "Index Record",
            RecordType.Blob => "BLOB Fragment",
            RecordType.GhostIndex => "Ghost Index Record",
            RecordType.GhostData => "Ghost Data Record",
            RecordType.GhostRecordVersion => "Ghost Record Version",
            _ => "Unknown"
        };
    }
}