using System.Data;

namespace InternalsViewer.Internals.Metadata
{
    /// <summary>
    /// Database Index or Table column
    /// </summary>
    public class Column
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Column"/> class.
        /// </summary>
        public Column()
        {
        }

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
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

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Column"/> is a uniqueifer.
        /// </summary>
        /// <value><c>true</c> if a uniqueifer; otherwise, <c>false</c>.</value>
        public bool Uniqueifer { get; set; }

        /// <summary>
        /// Gets or sets the name of the column.
        /// </summary>
        /// <value>The name of the column.</value>
        public string ColumnName { get; set; }

        /// <summary>
        /// Gets or sets the column id.
        /// </summary>
        /// <value>The column id.</value>
        public int ColumnId { get; set; }

        /// <summary>
        /// Gets or sets the data type
        /// </summary>
        /// <value>The data type.</value>
        public SqlDbType DataType { get; set; }

        /// <summary>
        /// Gets or sets the data length
        /// </summary>
        /// <value>The length of the data.</value>
        public short DataLength { get; set; } = 0;

        /// <summary>
        /// Gets or sets the leaf offset.
        /// </summary>
        /// <value>The leaf offset.</value>
        public short LeafOffset { get; set; }

        /// <summary>
        /// Gets or sets the precision.
        /// </summary>
        /// <value>The precision.</value>
        public byte Precision { get; set; }

        /// <summary>
        /// Gets or sets the scale.
        /// </summary>
        /// <value>The scale.</value>
        public byte Scale { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Column"/> is dropped.
        /// </summary>
        /// <value><c>true</c> if dropped; otherwise, <c>false</c>.</value>
        public bool Dropped { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Column"/> is sparse.
        /// </summary>
        /// <value><c>true</c> if sparse; otherwise, <c>false</c>.</value>
        public bool Sparse { get; set; }

        /// <summary>
        /// Gets or sets the index of null bit.
        /// </summary>
        /// <value>The index of null bit.</value>
        public short NullBit { get; set; }

        #endregion
    }
}
