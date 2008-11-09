using System;
using System.Data;

namespace InternalsViewer.Internals.Structures
{
    /// <summary>
    /// Database Index or Table column
    /// </summary>
    public class Column
    {
        private int columnId;
        private string columnName;
        private Int16 dataLength = 0;
        private SqlDbType dataType;
        private bool dropped;
        private Int16 leafOffset;
        private byte precision;
        private byte scale;
        private bool uniqueifer;
        private bool sparse;
        private Int16 nullBit;

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
        public bool Uniqueifer
        {
            get { return uniqueifer; }
            set { uniqueifer = value; }
        }

        /// <summary>
        /// Gets or sets the name of the column.
        /// </summary>
        /// <value>The name of the column.</value>
        public string ColumnName
        {
            get { return columnName; }
            set { columnName = value; }
        }

        /// <summary>
        /// Gets or sets the column id.
        /// </summary>
        /// <value>The column id.</value>
        public int ColumnId
        {
            get { return columnId; }
            set { columnId = value; }
        }

        /// <summary>
        /// Gets or sets the data type
        /// </summary>
        /// <value>The data type.</value>
        public SqlDbType DataType
        {
            get { return dataType; }
            set { dataType = value; }
        }

        /// <summary>
        /// Gets or sets the data length
        /// </summary>
        /// <value>The length of the data.</value>
        public Int16 DataLength
        {
            get { return dataLength; }
            set { dataLength = value; }
        }

        /// <summary>
        /// Gets or sets the leaf offset.
        /// </summary>
        /// <value>The leaf offset.</value>
        public Int16 LeafOffset
        {
            get { return leafOffset; }
            set { leafOffset = value; }
        }

        /// <summary>
        /// Gets or sets the precision.
        /// </summary>
        /// <value>The precision.</value>
        public byte Precision
        {
            get { return precision; }
            set { precision = value; }
        }

        /// <summary>
        /// Gets or sets the scale.
        /// </summary>
        /// <value>The scale.</value>
        public byte Scale
        {
            get { return scale; }
            set { scale = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Column"/> is dropped.
        /// </summary>
        /// <value><c>true</c> if dropped; otherwise, <c>false</c>.</value>
        public bool Dropped
        {
            get { return dropped; }
            set { dropped = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Column"/> is sparse.
        /// </summary>
        /// <value><c>true</c> if sparse; otherwise, <c>false</c>.</value>
        public bool Sparse
        {
            get { return sparse; }
            set { sparse = value; }
        }

        /// <summary>
        /// Gets or sets the index of null bit.
        /// </summary>
        /// <value>The index of null bit.</value>
        public Int16 NullBit
        {
            get { return nullBit; }
            set { nullBit = value; }
        }

        #endregion
    }
}
