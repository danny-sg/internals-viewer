using InternalsViewer.Internals.BlobPointers;
using InternalsViewer.Internals.Structures;

namespace InternalsViewer.Internals.Records
{
    /// <summary>
    /// Record field
    /// </summary>
    public class RecordField : Field
    {
        private BlobField blobInlineRoot;
        private Column column;
        private byte[] data;
        private int length;
        private int offset;
        private int variableOffset;
        private bool compressed;
        private bool sparse;

        /// <summary>
        /// Initializes a new instance of the <see cref="RecordField"/> class.
        /// </summary>
        /// <param name="column">The column.</param>
        public RecordField(Column column)
        {
            this.column = column;
        }

        public BlobField BlobInlineRoot
        {
            get { return this.blobInlineRoot; }
            set { this.blobInlineRoot = value; }
        }

        public Column Column
        {
            get { return this.column; }
            set { this.column = value; }
        }

        public int LeafOffset
        {
            get { return this.column.LeafOffset; }
        }

        public int Length
        {
            get { return this.length; }
            set { this.length = value; }
        }

        public int Offset
        {
            get { return this.offset; }
            set { this.offset = value; }
        }

        public byte[] Data
        {
            get { return this.data; }
            set { this.data = value; }
        }

        public int VariableOffset
        {
            get { return this.variableOffset; }
            set { this.variableOffset = value; }
        }

        public bool Compressed
        {
            get { return this.compressed; }
            set { this.compressed = value; }
        }

        public bool Sparse
        {
            get { return this.sparse; }
            set { this.sparse = value; }
        }

        public string Name
        {
            get { return this.Column.ColumnName; }
        }

        [MarkAttribute("", "Gray", "LemonChiffon", "PaleGoldenrod", true)]
        public string Value
        {
            get
            {
                return DataConverter.BinaryToString(this.Data, this.Column.DataType, this.Column.Precision, this.Column.Scale);
            }
        }

        public override string ToString()
        {
            return string.Format("  Offset: {0, -4} Leaf Offset: {1, -4} Length: {2, -4} Field: {3, -30} Data type: {4, -10} Value: {5}",
                                 this.Offset,
                                 this.LeafOffset,
                                 this.Length,
                                 this.Column.ColumnName,
                                 this.Column.DataType,
                                 DataConverter.ToHexString(this.Data));
        }
    }
}
