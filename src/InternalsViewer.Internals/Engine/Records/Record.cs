using System.Collections;
using System.Text;
using InternalsViewer.Internals.Engine.Annotations;
using InternalsViewer.Internals.Metadata;

namespace InternalsViewer.Internals.Engine.Records;

/// <summary>
/// Database Record Structure
/// </summary>
public abstract class Record : DataStructure
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
            RecordType.Forwarding => "Forwarding Record",
            RecordType.Index => "Index Record",
            RecordType.Blob => "BLOB Fragment",
            RecordType.GhostIndex => "Ghost Index Record",
            RecordType.GhostData => "Ghost Data Record",
            RecordType.GhostRecordVersion => "Ghost Record Version",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Gets a string representation of the null bitmap
    /// </summary>
    protected static string GetNullBitmapString(BitArray nullBitmap)
    {
        var stringBuilder = new StringBuilder();

        for (var i = 0; i < nullBitmap.Length; i++)
        {
            stringBuilder.Insert(0, nullBitmap[i] ? "1" : "0");
        }

        return stringBuilder.ToString();
    }

    public static string GetArrayString(ushort[] array)
    {
        var sb = new StringBuilder();

        foreach (var offset in array)
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            sb.AppendFormat("{0} - 0x{0:X}", offset);
        }

        return sb.ToString();
    }

    public bool NullBitmapValue(short index)
    {
        if (false) // TODO: has sparse column...
        {
            return false;
        }

        return NullBitmap.Get(index - (HasUniqueifier ? 0 : 1));
    }

    public bool IsNullBitmapSet(ColumnStructure columnStructure)
    {
        if (columnStructure.NullBit < 1)
        {
            return false;
        }

        return NullBitmap.Get(columnStructure.NullBit - 1);
    }

    internal static string GetStatusBitsDescription(Record record)
    {
        var statusDescription = string.Empty;

        if (record.HasVariableLengthColumns)
        {
            statusDescription += ", Variable Length Flag";
        }

        if (record is { HasNullBitmap: true, HasVariableLengthColumns: true })
        {
            statusDescription += " | NULL Bitmap Flag";
        }
        else if (record.HasNullBitmap)
        {
            statusDescription += ", NULL Bitmap Flag";
        }

        return statusDescription;
    }

    public RecordType RecordType { get; set; }

    [DataStructureItem(DataStructureItemType.SlotOffset)]
    public ushort SlotOffset { get; set; }

    public ushort[] ColOffsetArray { get; set; } = Array.Empty<ushort>();

    [DataStructureItem(DataStructureItemType.ColumnOffsetArray)]
    public string ColOffsetArrayDescription => GetArrayString(ColOffsetArray);

    public BitArray StatusBitsA { get; set; } = new(0);

    [DataStructureItem(DataStructureItemType.StatusBitsA)]
    public string StatusBitsADescription => GetRecordTypeDescription(RecordType) + GetStatusBitsDescription(this);

    public BitArray StatusBitsB { get; set; } = new(0);

    public short ColumnCountBytes { get; set; }

    [DataStructureItem(DataStructureItemType.ColumnCount)]
    public short ColumnCount { get; set; }

    [DataStructureItem(DataStructureItemType.ColumnCountOffset)]
    public short ColumnCountOffset { get; set; }

    public bool HasVariableLengthColumns { get; set; }

    public ushort VariableLengthDataOffset { get; set; }

    [DataStructureItem(DataStructureItemType.VariableLengthColumnCount)]
    public ushort VariableLengthColumnCount { get; set; }

    public bool HasNullBitmap { get; set; }

    public short NullBitmapSize { get; set; }

    public BitArray NullBitmap { get; set; } = new(0);

    [DataStructureItem(DataStructureItemType.NullBitmap)]
    public string NullBitmapDescription => HasNullBitmap ? GetNullBitmapString(NullBitmap) : string.Empty;

    public bool HasUniqueifier { get; set; }

    public bool Compressed { get; set; }

    public List<RecordField> Fields { get; set; } = new();

    public RecordField[] FieldsArray => Fields.ToArray();
}