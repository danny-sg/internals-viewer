using System.Data;

namespace InternalsViewer.Internals.Metadata;

/// <summary>
/// Database Index or Table column physical structure
/// </summary>
public class ColumnStructure
{
    /// <summary>
    ///Name of the column.
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>
    /// Column id.
    /// </summary>
    public int ColumnId { get; set; }

    /// <summary>
    /// Data type
    /// </summary>
    public SqlDbType DataType { get; set; }

    /// <summary>
    /// Data length if fixed length
    /// </summary>
    public short DataLength { get; set; } = 0;

    /// <summary>
    /// Leaf offset for fixed length fields
    /// </summary>
    public short LeafOffset { get; set; }

    /// <summary>
    /// Precision (used for decimal/date/time types)
    /// </summary>
    public byte Precision { get; set; }

    /// <summary>
    /// Scale (used for decimal/date/time types)
    /// </summary>
    public byte Scale { get; set; }

    /// <summary>
    /// If the column has been dropped
    /// </summary>
    public bool IsDropped { get; set; }

    /// <summary>
    /// If the column is sparse
    /// </summary>
    public bool IsSparse { get; set; }

    /// <summary>
    /// If the column is a uniqueifier (special hidden column to ensure clustered index rows are unique)
    /// </summary>
    public bool IsUniqueifier { get; set; }

    /// <summary>
    /// 1-based index for the column in the null bitmap.
    /// </summary>
    public short NullBitIndex { get; set; }

    /// <summary>
    /// Bit position for a BIT column as bits are stored in bytes, up to 8 bits/columns per byte.
    /// </summary>
    public short BitPosition { get; set; }
}