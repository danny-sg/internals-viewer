using System.Diagnostics;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Engine.Records.CdRecordType;

internal class CompressedRecordField(ColumnStructure columnStructure, CompressedDataRecord parentRecord) : RecordField(columnStructure)
{
    /// <summary>
    /// Uses the anchor field to expand the compressed data
    /// </summary>
    /// <remarks>
    /// In row compression the anchor record represents the longest prefix that could be used. Values don't necessarily use the whole 
    /// anchor value.
    /// 
    /// The length of the anchor is determined by the first one or two bytes of the data. If the first bit is set, the anchor length is 2 
    /// bytes long, else it is 1 byte long.
    /// 
    /// The anchor length determines how many bytes of the anchor is used, then the rest of the data is appended.
    /// </remarks>
    /// <param name="fieldData">
    /// The field data representing the length of the anchor and suffix data
    /// </param>
    public byte[] ExpandAnchor(byte[] fieldData)
    {
        AnchorLength = CompressedDataConverter.DecodeInternalInt(fieldData, 0);

        // Determine how many bytes have been used for the anchor length value
        var dataOffset = (fieldData[0] & 0x80) != 0 ? 2 : 1;

        var compositeData = new byte[AnchorLength + fieldData.Length - dataOffset];

        Debug.Assert(AnchorField != null, nameof(AnchorField) + " != null");

        // Copy the anchor prefix up to the length specified
        Array.Copy(AnchorField.Data, 0, compositeData, 0, AnchorLength);

        // Copy the data as suffix to the anchor
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

    public override string Value
    {
        get
        {
            if (IsPageSymbol)
            {
                return GetPageSymbolValue();
            }

            if (AnchorField is { Data: not null, IsNull: false })
            {
                return GetValueWithAnchor();
            }

            return CompressedDataConverter.CompressedBinaryToBinary(Data,
                                                                    ColumnStructure.DataType,
                                                                    ColumnStructure.Precision,
                                                                    ColumnStructure.Scale);
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

        if (AnchorField is { Data: not null } && Data.Length > 1)
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
