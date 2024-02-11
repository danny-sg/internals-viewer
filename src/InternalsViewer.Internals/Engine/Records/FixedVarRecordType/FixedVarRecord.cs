using System.Collections;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Engine.Records.FixedVarRecordType;

public abstract class FixedVarRecord : Record
{
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

    public bool IsNullBitmapSet(ColumnStructure columnStructure, int offset)
    {
        if (columnStructure.NullBitIndex < 1 || NullBitmap.Length < columnStructure.NullBitIndex)
        {
            return false;
        }

        return NullBitmap.Get(columnStructure.NullBitIndex - 1 + offset);
    }

    [DataStructureItem(ItemType.VariableLengthColumnOffsetArray)]
    public ushort[] VariableLengthColumnOffsetArray { get; set; } = Array.Empty<ushort>();

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
    public BitArray NullBitmap { get; set; } = new(0);

    public bool HasUniqueifier { get; set; }

    public bool HasRowVersioning { get; set; }
}