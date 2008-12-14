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

        public CompressedRecordField(Column column, CompressedDataRecord parentRecord)
            : base(column)
        {
            this.Record = parentRecord;
        }

        public byte[] ExpandAnchor(byte[] fieldData)
        {
            this.AnchorLength = CompressedDataConverter.DecodeInternalInt(fieldData, 0);

            int dataOffset = ((fieldData[0] & 0x80) == 0x80) ? 2 : 1;

            byte[] compositeData = new byte[this.AnchorLength + fieldData.Length - dataOffset];

            Array.Copy(this.AnchorField.Data, 0, compositeData, 0, this.AnchorLength);
            Array.Copy(fieldData, dataOffset, compositeData, this.AnchorLength, fieldData.Length - dataOffset);

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

        [MarkAttribute("", "Black", "PaleGreen", "LightGreen", true)]
        public string Value
        {
            get
            {
                if (this.PageSymbol)
                {
                    return GetPageSymbolValue();
                }
                else if (field.AnchorField != null && field.AnchorField.Data != null)
                {
                    return GetValueFromAnchor();
                }
                else
                {
                    return CompressedDataConverter.CompressedBinaryToBinary(this.Data,
                                                                            this.Column.DataType,
                                                                            this.Column.Precision,
                                                                            this.Column.Scale) ?? String.Empty;
                }
            }
        }

        private string GetValueFromAnchor()
        {
            if (this.Data.Length > 0)
            {
                byte[] compositeData = this.ExpandAnchor(this.Data);

                Marker anchorOffsetMarker = new Marker(string.Format("{0} data offset", field.Column.ColumnName),
                                                Marker.MarkerType.Compressed,
                                                offset,
                                                offset + field.DataOffset - 1,
                                                Color.LightGreen,
                                                Color.Gray);

                anchorOffsetMarker.Value = field.AnchorLength.ToString();

                fieldMarker.StartPosition += field.DataOffset;
                fieldMarker.EndPosition += field.DataOffset - 1;

                markers.Add(anchorOffsetMarker);

                return CompressedDataConverter.CompressedBinaryToBinary(compositeData,
                                                                                     field.Column.DataType,
                                                                                     field.Column.Precision,
                                                                                     field.Column.Scale);
            }
            else
            {
                return CompressedDataConverter.CompressedBinaryToBinary(field.AnchorField.Data,
                                                 field.Column.DataType,
                                                 field.Column.Precision,
                                                 field.Column.Scale);
            }
        }

        private string GetPageSymbolValue()
        {
            int dictionaryEntry = CompressedDataConverter.DecodeInternalInt(this.Data, 0);
            byte[] dictionaryValue = this.Record.Page.CompressionInformation.CompressionDictionary.DictionaryEntries[dictionaryEntry].Data;

            string value;

            if (this.AnchorField != null && this.AnchorField.Data != null)
            {
                byte[] compositeData = this.ExpandAnchor(dictionaryValue);

                value = CompressedDataConverter.CompressedBinaryToBinary(compositeData,
                                                                         this.Column.DataType,
                                                                         this.Column.Precision,
                                                                         this.Column.Scale);
            }
            else
            {
                value = CompressedDataConverter.CompressedBinaryToBinary(dictionaryValue,
                                                                         this.Column.DataType,
                                                                         this.Column.Precision,
                                                                         this.Column.Scale);
            }

            return string.Format("Dictionary Entry {0} - {1}", dictionaryEntry, value);
        }
    }
}
