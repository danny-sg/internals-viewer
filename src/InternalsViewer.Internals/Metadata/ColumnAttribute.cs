using System.Data;

namespace InternalsViewer.Internals.Metadata;

[AttributeUsage(AttributeTargets.Property)]
public class ColumnAttribute : Attribute
{
    /// <summary>
    /// Name of the column.
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
    /// Data length
    /// </summary>
    public short DataLength { get; set; } = 0;

    /// <summary>
    /// Leaf offset for fixed length fields
    /// </summary>
    public short LeafOffset { get; set; }

    /// <summary>
    /// 1-based index for the column in the null bitmap.
    /// </summary>
    public short NullBitIndex { get; set; }
}