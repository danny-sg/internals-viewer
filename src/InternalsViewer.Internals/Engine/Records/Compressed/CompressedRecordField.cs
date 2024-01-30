using System.Diagnostics;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Engine.Annotations;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Engine.Records.Compressed;

internal class CompressedRecordField(ColumnStructure columnStructure, CompressedDataRecord parentRecord) : RecordField(columnStructure)
{
    public byte[] ExpandAnchor(byte[] fieldData)
    {
        AnchorLength = CompressedDataConverter.DecodeInternalInt(fieldData, 0);

        var dataOffset = ((fieldData[0] & 0x80) == 0x80) ? 2 : 1;

        var compositeData = new byte[AnchorLength + fieldData.Length - dataOffset];

        Debug.Assert(AnchorField != null, nameof(AnchorField) + " != null");

        Array.Copy(AnchorField.Data, 0, compositeData, 0, AnchorLength);
        Array.Copy(fieldData, dataOffset, compositeData, AnchorLength, fieldData.Length - dataOffset);

        return compositeData;
    }

    public bool IsNull { get; set; }

    public int Cluster { get; set; }

    public int AnchorLength { get; set; }

    public CompressedRecordField? AnchorField { get; set; }

    public CompressedDataRecord Record { get; set; } = parentRecord;

    public short SymbolOffset { get; set; }

    public bool IsPageSymbol { get; set; }

    [DataStructureItem(ItemType.CompressedValue)]
    public override string Value
    {
        get
        {
            if (IsPageSymbol)
            {
                return GetPageSymbolValue();
            }

            if (AnchorField is { Data: not null })
            {
                return GetValueWithAnchor();
            }

            return CompressedDataConverter.CompressedBinaryToBinary(Data,
                                                                    ColumnStructure.DataType,
                                                                    ColumnStructure.Precision,
                                                                    ColumnStructure.Scale) ?? string.Empty;
        }
    }

    private string GetValueWithAnchor()
    {
        if (Data.Length > 0)
        {
            var compositeData = ExpandAnchor(Data);

            return CompressedDataConverter.CompressedBinaryToBinary(compositeData,
                                                                    ColumnStructure.DataType,
                                                                    ColumnStructure.Precision,
                                                                    ColumnStructure.Scale);
        }

        return CompressedDataConverter.CompressedBinaryToBinary(AnchorField!.Data,
                                                                ColumnStructure.DataType,
                                                                ColumnStructure.Precision,
                                                                ColumnStructure.Scale);
    }

    private string GetPageSymbolValue()
    {
        var dictionaryEntry = CompressedDataConverter.DecodeInternalInt(Data, 0);
        var dictionaryValue = Record.CompressionInfo.CompressionDictionary?.DictionaryEntries[dictionaryEntry].Data ?? Array.Empty<byte>();

        string value;

        if (AnchorField is { Data: not null })
        {
            var compositeData = ExpandAnchor(dictionaryValue);

            value = CompressedDataConverter.CompressedBinaryToBinary(compositeData,
                ColumnStructure.DataType,
                ColumnStructure.Precision,
                ColumnStructure.Scale);
        }
        else
        {
            value = CompressedDataConverter.CompressedBinaryToBinary(dictionaryValue,
                ColumnStructure.DataType,
                ColumnStructure.Precision,
                ColumnStructure.Scale);
        }

        return $"Dictionary Entry {dictionaryEntry} - {value}";
    }
}
