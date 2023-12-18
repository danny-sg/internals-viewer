using System.Data;

namespace InternalsViewer.Internals.Metadata;

/// <summary>
/// Database Index or Table column
/// </summary>
public class ColumnStructure
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
    /// Gets or sets the precision.
    /// </summary>
    public byte Precision { get; set; }

    /// <summary>
    /// Gets or sets the scale.
    /// </summary>
    public byte Scale { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="ColumnStructure"/> is dropped.
    /// </summary>
    public bool IsDropped { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="ColumnStructure"/> is sparse.
    /// </summary>
    public bool IsSparse { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="ColumnStructure"/> is a uniqueifer.
    /// </summary>
    public bool IsUniqueifer { get; set; }

    /// <summary>
    /// Gets or sets the index of the null bit.
    /// </summary>
    public short NullBit { get; set; }
}