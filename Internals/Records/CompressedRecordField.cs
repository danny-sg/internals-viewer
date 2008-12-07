using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.Structures;

namespace InternalsViewer.Internals.Records
{
    class CompressedRecordField : RecordField
    {
        private bool pageSymbol;
        private short symbolOffset;
        private CompressedDataRecord record;
        private RecordField anchorField;
        private int anchorLength;
        private bool isNull;
        private int dataOffset = 0;

        public CompressedRecordField(Column column, CompressedDataRecord parentRecord)
            : base(column)
        {
            this.Record = parentRecord;
        }

        public byte[] ExpandAnchor()
        {
            this.AnchorLength = CompressedDataConverter.DecodeInternalInt(this.Data, 0);
            this.DataOffset = ((this.Data[0] & 0x80) == 0x80) ? 2 : 1;

            byte[] compositeData = new byte[this.AnchorLength + this.Data.Length - this.DataOffset];

            Array.Copy(this.AnchorField.Data, 0, compositeData, 0, this.AnchorLength);
            Array.Copy(this.Data, this.DataOffset, compositeData, this.AnchorLength, this.Data.Length - this.DataOffset);

            return compositeData;
        }

        public bool IsNull
        {
            get { return this.isNull; }
            set { this.isNull = value; }
        }
        public int AnchorLength
        {
            get { return this.anchorLength; }
            set { this.anchorLength = value; }
        }

        public int DataOffset
        {
            get { return this.dataOffset; }
            set { this.dataOffset = value; }
        }

        public RecordField AnchorField
        {
            get { return this.anchorField; }
            set { this.anchorField = value; }
        }

        public CompressedDataRecord Record
        {
            get { return this.record; }
            set { this.record = value; }
        }

        public short SymbolOffset
        {
            get { return this.symbolOffset; }
            set { this.symbolOffset = value; }
        }

        public bool PageSymbol
        {
            get { return this.pageSymbol; }
            set { this.pageSymbol = value; }
        }

        [MarkAttribute("", "Gray", "LemonChiffon", "PaleGoldenrod", true)]
        public string Value
        {
            get
            {
                if (this.Length < 1)
                {
                    if (!this.IsNull && this.AnchorField != null)
                    {
                        return CompressedDataConverter.CompressedBinaryToBinary(this.AnchorField.Data,
                                                                                this.Column.DataType,
                                                                                this.Column.Precision,
                                                                                this.Column.Scale);
                    }

                }
                return "";
            }
        }
    }
}
