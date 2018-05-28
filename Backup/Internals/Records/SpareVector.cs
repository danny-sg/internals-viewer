using System;
using InternalsViewer.Internals.RecordLoaders;
using InternalsViewer.Internals.Structures;

namespace InternalsViewer.Internals.Records
{
    public class SparseVector: Markable
    {
        private byte[] data;
        private TableStructure structure;

        private UInt16[] columns;
        private UInt16[] offset;
        private Int16 colCount;
        private DataRecord parentRecord;
        private Int16 complexHeader;
        private Int16 recordOffset;

        public const int ColCountOffset = 2;
        public const int ColumnsOffset = 4;

        internal SparseVector(byte[] sparseRecord, TableStructure structure, DataRecord parentRecord, Int16 recordOffset)
        {
            this.data = sparseRecord;
            this.structure = structure;
            this.parentRecord = parentRecord;
            this.recordOffset = recordOffset;

            SparseVectorLoader.Load(this);
        }

        public static string GetComplexHeaderDescription(Int16 complexVector)
        {
            switch (complexVector)
            {
                case 5:
                    return "In row sparse vector";
                default:
                    return "Unknown";
            }
        }

        internal TableStructure Structure
        {
            get { return structure; }
            set { structure = value; }
        }

        public byte[] Data
        {
            get { return data; }
            set { data = value; }
        }

        internal DataRecord ParentRecord
        {
            get { return this.parentRecord; }
            set { this.parentRecord = value; }
        }

        public UInt16[] Columns
        {
            get { return this.columns; }
            set { this.columns = value; }
        }

        [MarkAttribute("Sparse Columns", "Black", "Olive", true)]
        public string ColumnsDescription
        {
            get { return Record.GetArrayString(this.Columns); }
        }

        [MarkAttribute("Sparse Column Offsets", "Black", "DarkKhaki", true)]
        public string OffsetsDescription
        {
            get { return Record.GetArrayString(this.Offset); }
        }

        public UInt16[] Offset
        {
            get { return this.offset; }
            set { this.offset = value; }
        }

        [MarkAttribute("Sparse Column Count", "Black", "SeaGreen", true)]
        public Int16 ColCount
        {
            get { return this.colCount; }
            set { this.colCount = value; }
        }

        public Int16 RecordOffset
        {
            get { return this.recordOffset; }
            set { this.recordOffset = value; }
        }

        public Int16 ComplexHeader
        {
            get { return this.complexHeader; }
            set { this.complexHeader = value; }
        }

        [MarkAttribute("Complex Header", "Green", "Gainsboro", true)]
        public string ComplexHeaderDescription
        {
            get { return GetComplexHeaderDescription(this.ComplexHeader); }
        }


    }
}
