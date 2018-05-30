using System;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.Engine.Records.Compressed
{
    class CompressedRecordField : RecordField
    {
        public CompressedRecordField(Column column, CompressedDataRecord parentRecord)
            : base(column)
        {
            Record = parentRecord;
        }

        public byte[] ExpandAnchor(byte[] fieldData)
        {
            AnchorLength = CompressedDataConverter.DecodeInternalInt(fieldData, 0);

            var dataOffset = ((fieldData[0] & 0x80) == 0x80) ? 2 : 1;

            var compositeData = new byte[AnchorLength + fieldData.Length - dataOffset];

            Array.Copy(AnchorField.Data, 0, compositeData, 0, AnchorLength);
            Array.Copy(fieldData, dataOffset, compositeData, AnchorLength, fieldData.Length - dataOffset);

            return compositeData;
        }

        public bool IsNull { get; set; }

        public int AnchorLength { get; set; }

        public RecordField AnchorField { get; set; }

        public CompressedDataRecord Record { get; set; }

        public short SymbolOffset { get; set; }

        public bool PageSymbol { get; set; }

        [Mark(MarkType.CompressedValue)]
        public new string Value
        {
            get
            {
                if (PageSymbol)
                {
                    return GetPageSymbolValue();
                }
                else if (AnchorField != null && AnchorField.Data != null)
                {
                    return GetValueWithAnchor();
                }
                else
                {
                    return CompressedDataConverter.CompressedBinaryToBinary(Data,
                                                                            Column.DataType,
                                                                            Column.Precision,
                                                                            Column.Scale) ?? string.Empty;
                }
            }
        }

        private string GetValueWithAnchor()
        {
            if (Data.Length > 0)
            {
                var compositeData = ExpandAnchor(Data);

                return CompressedDataConverter.CompressedBinaryToBinary(compositeData,
                                                                        Column.DataType,
                                                                        Column.Precision,
                                                                        Column.Scale);
            }
            else
            {
                return CompressedDataConverter.CompressedBinaryToBinary(AnchorField.Data,
                                                                        Column.DataType,
                                                                        Column.Precision,
                                                                        Column.Scale);
            }
        }

        private string GetPageSymbolValue()
        {
            var dictionaryEntry = CompressedDataConverter.DecodeInternalInt(Data, 0);
            var dictionaryValue = Record.Page.CompressionInformation.CompressionDictionary.DictionaryEntries[dictionaryEntry].Data;

            string value;

            if (AnchorField != null && AnchorField.Data != null)
            {
                var compositeData = ExpandAnchor(dictionaryValue);

                value = CompressedDataConverter.CompressedBinaryToBinary(compositeData,
                                                                         Column.DataType,
                                                                         Column.Precision,
                                                                         Column.Scale);
            }
            else
            {
                value = CompressedDataConverter.CompressedBinaryToBinary(dictionaryValue,
                                                                         Column.DataType,
                                                                         Column.Precision,
                                                                         Column.Scale);
            }

            return string.Format("Dictionary Entry {0} - {1}", dictionaryEntry, value);
        }
    }
}
