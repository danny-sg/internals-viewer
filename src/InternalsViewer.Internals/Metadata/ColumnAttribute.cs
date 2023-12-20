using System.Data;

namespace InternalsViewer.Internals.Metadata;

[AttributeUsage(AttributeTargets.Property)]
public class ColumnAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the name of the column.
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the column id.
    /// </summary>
    public int ColumnId { get; set; }

    /// <summary>
    /// Gets or sets the data type
    /// </summary>
    public SqlDbType DataType { get; set; }

    /// <summary>
    /// Gets or sets the data length
    /// </summary>
    public short DataLength { get; set; } = 0;

    /// <summary>
    /// Gets or sets the leaf offset.
    /// </summary>
    public short LeafOffset { get; set; }

    /// <summary>
    /// Gets or sets the index of the null bit.
    /// </summary>
    public short NullBit { get; set; }
}