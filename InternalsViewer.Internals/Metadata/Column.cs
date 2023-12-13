using System.Data;

namespace InternalsViewer.Internals.Metadata;

/// <summary>
/// Database Index or Table column
/// </summary>
public class Column
{
    public override string ToString()
    {
        return string.Format("Column: {0,-40} Column ID: {1,-5} Data Type: {2,-20} Data Length: {3, -5} Leaf Offset {4,-5} Precision {5,-5} Scale {6,-5}",
            ColumnName,
            ColumnId,
            DataType,
            DataLength,
            LeafOffset,
            Precision,
            Scale);
    }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="Column"/> is a uniqueifer.
    /// </summary>
    public bool Uniqueifer { get; set; }

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
    /// Gets or sets a value indicating whether this <see cref="Column"/> is dropped.
    /// </summary>
    public bool Dropped { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="Column"/> is sparse.
    /// </summary>
    public bool Sparse { get; set; }

    /// <summary>
    /// Gets or sets the index of the null bit.
    /// </summary>
    public short NullBit { get; set; }
}