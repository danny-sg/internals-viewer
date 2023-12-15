using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Records;

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

    /// <summary>
    /// Get a specific null bitmap value
    /// </summary>
    public bool NullBitmapValue(short index)
    {
        if (false) // TODO: has sparse column...
        {
            return false;
        }

        return NullBitmap.Get(index - (HasUniqueifier ? 0 : 1));
    }

    public bool NullBitmapValue(ColumnStructure columnStructure)
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

    /// <summary>
    /// Gets or sets the record type
    /// </summary>
    public RecordType RecordType { get; set; }

    /// <summary>
    /// Gets or sets the slot offset in the page
    /// </summary>
    [DataStructureItem(DataStructureItemType.SlotOffset)]
    public ushort SlotOffset { get; set; }

    /// <summary>
    /// Gets or sets the Column Offset Array
    /// </summary>
    public ushort[] ColOffsetArray { get; set; } = Array.Empty<ushort>();

    [DataStructureItem(DataStructureItemType.ColumnOffsetArray)]
    public string ColOffsetArrayDescription => GetArrayString(ColOffsetArray);

    /// <summary>
    /// Gets or sets the status bits A value (bitmap of row properties)
    /// </summary>
    public BitArray StatusBitsA { get; set; } = new(0);

    [DataStructureItem(DataStructureItemType.StatusBitsA)]
    public string StatusBitsADescription => GetRecordTypeDescription(RecordType) + GetStatusBitsDescription(this);

    /// <summary>
    /// Gets or sets the status bits B value (bitmap of row properties)
    /// </summary>
    public BitArray StatusBitsB { get; set; } = new(0);

    /// <summary>
    /// Gets or sets the column count bytes value
    /// </summary>
    /// <value>The number of bytes used for the column count.</value>
    /// <remarks>Used for SQL Server 2008 page compression</remarks>
    public short ColumnCountBytes { get; set; }

    /// <summary>
    /// Gets or sets the number of columns in the record
    /// </summary>
    [DataStructureItem(DataStructureItemType.ColumnCount)]
    public short ColumnCount { get; set; }

    /// <summary>
    /// Gets or sets the fixed column offset.
    /// </summary>
    /// <value>The offset location of the start of the fixed column fields</value>
    [DataStructureItem(DataStructureItemType.ColumnCountOffset)]
    public short ColumnCountOffset { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance has variable length columns.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if this instance has variable length columns; otherwise, <c>false</c>.
    /// </value>
    public bool HasVariableLengthColumns { get; set; }

    /// <summary>
    /// Gets or sets the variable length data offset.
    /// </summary>
    public ushort VariableLengthDataOffset { get; set; }

    /// <summary>
    /// Gets or sets the variable length column count.
    /// </summary>
    [DataStructureItem(DataStructureItemType.VariableLengthColumnCount)]
    public ushort VariableLengthColumnCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance has a null bitmap.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if this instance has null bitmap; otherwise, <c>false</c>.
    /// </value>
    public bool HasNullBitmap { get; set; }

    /// <summary>
    /// Gets or sets the size of the null bitmap in bytes
    /// </summary>
    public short NullBitmapSize { get; set; }

    /// <summary>
    /// Gets or sets the null bitmap.
    /// </summary>
    public BitArray NullBitmap { get; set; } = new(0);

    [DataStructureItem(DataStructureItemType.NullBitmap)]
    public string NullBitmapDescription => HasNullBitmap ? GetNullBitmapString(NullBitmap) : string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this instance has a uniqueifier.
    /// </summary>
    public bool HasUniqueifier { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="Record"/> is compressed.
    /// </summary>
    public bool Compressed { get; set; }

    /// <summary>
    /// Gets or sets the record fields.
    /// </summary>
    /// <value>The record fields.</value>
    public List<RecordField> Fields { get; set; } = new();

    public RecordField[] FieldsArray => Fields.ToArray();
}