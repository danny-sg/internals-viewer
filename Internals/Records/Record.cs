using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Structures;

namespace InternalsViewer.Internals.Records
{
    /// <summary>
    /// Database Record Stucture
    /// </summary>
    public abstract class Record: IMarkable
    {
        private Page page;
        private RecordType recordType;
        private UInt16 slotOffset;

        private UInt16[] colOffsetArray;

        private BitArray statusBitsA;
        private BitArray statusBitsB;

        private Int16 columnCountBytes;
        private Int16 columnCount;
        private Int16 columnCountOffset;
        private bool hasVariableLengthColumns;
        private UInt16 variableLengthDataOffset;
        private UInt16 variableLengthColumnCount;

        private bool hasNullBitmap;
        private Int16 nullBitmapSize;
        private BitArray nullBitmap;

        private bool hasUniqueifier;
        private bool compressed;

        private List<RecordField> fields;
        private Structure structure;
        private List<MarkItem> markItems = new List<MarkItem>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Record"/> class.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="offset">The offset.</param>
        public Record(Page page, UInt16 slotOffset, Structure structure)
        {
            this.Page = page;
            this.SlotOffset = slotOffset;
            this.Structure = structure;
            this.Fields = new List<RecordField>();
        }

        /// <summary>
        /// Gets the record type description.
        /// </summary>
        /// <param name="recordType">Type of the record.</param>
        /// <returns></returns>
        protected static string GetRecordTypeDescription(RecordType recordType)
        {
            switch (recordType)
            {
                case RecordType.Primary: return "Primary Record";
                case RecordType.Forwarded: return "Forwarded Record";
                case RecordType.Forwarding: return "Forwarding Record";
                case RecordType.Index: return "Index Record";
                case RecordType.Blob: return "BLOB Fragment";
                case RecordType.GhostIndex: return "Ghost Index Record";
                case RecordType.GhostData: return "Ghost Data Record";
                case RecordType.GhostRecordVersion: return "Ghost Record Version";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// Gets a string representation of the null bitmap
        /// </summary>
        /// <returns></returns>
        protected static string GetNullBitmapString(BitArray nullBitmap)
        {
            StringBuilder stringBuilder = new StringBuilder();

            for (int i = 0; i < nullBitmap.Length; i++)
            {
                stringBuilder.Insert(0, nullBitmap[i] ? "1" : "0");
            }

            return stringBuilder.ToString();
        }

        public static string GetArrayString(UInt16[] array)
        {
            StringBuilder sb = new StringBuilder();

            foreach (UInt16 offset in array)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.AppendFormat("{0} - 0x{0:X}", offset);
            }

            return sb.ToString();
        }

        public void Mark(string propertyName, int startPosition, int length)
        {
            this.markItems.Add(new MarkItem(propertyName, startPosition, length));
        }

        public void Mark(string propertyName, int startPosition, int length, int index)
        {
            this.markItems.Add(new MarkItem(propertyName, startPosition, length, index));
        }

        /// <summary>
        /// Get a specific null bitmap value
        /// </summary>
        /// <param name="columnOrdinal">The column ordinal.</param>
        /// <returns></returns>
        public bool NullBitmapValue(Int16 index)
        {
            if (false) // TODO: has sparse column...
            {
                return false;
            }
            else
            {
                return this.NullBitmap.Get(index - (this.HasUniqueifier ? 0 : 1));
            }
        }

        public bool NullBitmapValue(Column column)
        {
            if (column.NullBit < 1)
            {
                return false;
            }
            else
            {
                return this.NullBitmap.Get(column.NullBit - 1);
            }
        }

        #region Properties

        /// <summary>
        /// Gets or sets the record's underlying Page
        /// </summary>
        /// <value>The Page.</value>
        public Page Page
        {
            get { return this.page; }
            set { this.page = value; }
        }

        /// <summary>
        /// Gets or sets the record type
        /// </summary>
        /// <value>The type of the record.</value>
        public RecordType RecordType
        {
            get { return this.recordType; }
            set { this.recordType = value; }
        }

        /// <summary>
        /// Gets or sets the slot offset in the page
        /// </summary>
        /// <value>The slot offset.</value>
        [MarkAttribute("Slot Offset")]
        public UInt16 SlotOffset
        {
            get { return this.slotOffset; }
            set { this.slotOffset = value; }
        }

        /// <summary>
        /// Gets or sets the Column Offset Array
        /// </summary>
        /// <value>The col offset array.</value>
        public UInt16[] ColOffsetArray
        {
            get { return this.colOffsetArray; }
            set { this.colOffsetArray = value; }
        }

        [MarkAttribute("Column Offset Array", "Blue", "AliceBlue", true)]
        public string ColOffsetArrayDescription
        {
            get { return GetArrayString(this.ColOffsetArray); }
        }

        /// <summary>
        /// Gets or sets the status bits A value
        /// </summary>
        /// <value>The status bits A (bitmap of row properties) value </value>
        public BitArray StatusBitsA
        {
            get { return this.statusBitsA; }
            set { this.statusBitsA = value; }
        }

        /// <summary>
        /// Gets or sets the status bits B value
        /// </summary>
        /// <value>The status bits B (bitmap of row properties) value</value>
        public BitArray StatusBitsB
        {
            get { return this.statusBitsB; }
            set { this.statusBitsB = value; }
        }

        /// <summary>
        /// Gets or sets the column count bytes value
        /// </summary>
        /// <value>The number of bytes used for the column count.</value>
        /// <remarks>Used for SQL Server 2008 page compression</remarks>
        public Int16 ColumnCountBytes
        {
            get { return this.columnCountBytes; }
            set { this.columnCountBytes = value; }
        }

        /// <summary>
        /// Gets or sets the number of columns.
        /// </summary>
        /// <value>The number of columns in the record</value>
        [MarkAttribute("Column Count", "DarkGreen", "Gainsboro", true)]
        public Int16 ColumnCount
        {
            get { return this.columnCount; }
            set { this.columnCount = value; }
        }

        /// <summary>
        /// Gets or sets the fixed column offset.
        /// </summary>
        /// <value>The offset location of the start of the fixed column fields</value>
        [MarkAttribute("Column Count Offset", "Blue", "Gainsboro", true)]
        public Int16 ColumnCountOffset
        {
            get { return this.columnCountOffset; }
            set { this.columnCountOffset = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance has variable length columns.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance has variable length columns; otherwise, <c>false</c>.
        /// </value>
        public bool HasVariableLengthColumns
        {
            get { return this.hasVariableLengthColumns; }
            set { this.hasVariableLengthColumns = value; }
        }

        /// <summary>
        /// Gets or sets the variable length data offset.
        /// </summary>
        /// <value>The variable length data offset.</value>
        public UInt16 VariableLengthDataOffset
        {
            get { return this.variableLengthDataOffset; }
            set { this.variableLengthDataOffset = value; }
        }

        /// <summary>
        /// Gets or sets the variable length column count.
        /// </summary>
        /// <value>The variable length column count.</value>
        [MarkAttribute("Variable Length Column Count", "Black", "AliceBlue", true)]
        public UInt16 VariableLengthColumnCount
        {
            get { return this.variableLengthColumnCount; }
            set { this.variableLengthColumnCount = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance has a null bitmap.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance has null bitmap; otherwise, <c>false</c>.
        /// </value>
        public bool HasNullBitmap
        {
            get { return this.hasNullBitmap; }
            set { this.hasNullBitmap = value; }
        }

        /// <summary>
        /// Gets or sets the size of the null bitmap in bytes
        /// </summary>
        /// <value>The size of the null bitmap in bytes</value>
        public Int16 NullBitmapSize
        {
            get { return this.nullBitmapSize; }
            set { this.nullBitmapSize = value; }
        }

        /// <summary>
        /// Gets or sets the null bitmap.
        /// </summary>
        /// <value>The null bitmap.</value>
        public BitArray NullBitmap
        {
            get { return this.nullBitmap; }
            set { this.nullBitmap = value; }
        }

        [MarkAttribute("Null Bitmap", "Purple", "Gainsboro", true)]
        public string NullBitmapDescription
        {
            get { return this.HasNullBitmap ? GetNullBitmapString(this.NullBitmap) : string.Empty; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance has a uniqueifier.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance has a uniqueifier; otherwise, <c>false</c>.
        /// </value>
        public bool HasUniqueifier
        {
            get { return this.hasUniqueifier; }
            set { this.hasUniqueifier = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Record"/> is compressed.
        /// </summary>
        /// <value><c>true</c> if compressed; otherwise, <c>false</c>.</value>
        public bool Compressed
        {
            get { return this.compressed; }
            set { this.compressed = value; }
        }

        /// <summary>
        /// Gets or sets the record fields.
        /// </summary>
        /// <value>The record fields.</value>
        public List<RecordField> Fields
        {
            get { return this.fields; }
            set { this.fields = value; }
        }

        [MarkAttribute("[Field]", "Gray", "LemonChiffon", "PaleGoldenrod", true)]
        public RecordField[] FieldsArray
        {
            get { return this.Fields.ToArray(); }
        }


        /// <summary>
        /// Gets or sets the record structure.
        /// </summary>
        /// <value>The record structure.</value>
        public Structure Structure
        {
            get { return structure; }
            set { structure = value; }
        }

        public List<MarkItem> MarkItems
        {
            get { return markItems; }
        }

        #endregion

    }
}
