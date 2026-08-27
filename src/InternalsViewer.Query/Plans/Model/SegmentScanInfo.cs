namespace InternalsViewer.Query.Plans.Model;

public sealed class SegmentScanInfo
{
    public long RowGroupId { get; set; }

    public int ColumnId { get; set; }

    public string EncodingType { get; set; } = string.Empty;

    public string CompressedDataType { get; set; } = string.Empty;

    public string FilterType { get; set; } = string.Empty;

    public string FilterOnCompressedDataType { get; set; } = string.Empty;

    public string InstructionSet { get; set; } = string.Empty;

    public int BitPacking { get; set; }

    public long BaseId { get; set; }

    public double Magnitude { get; set; }

    public long MinDataId { get; set; }

    public long MaxDataId { get; set; }

    public uint PrimaryDictionaryValueCount { get; set; }

    public uint SecondaryDictionaryValueCount { get; set; }

    public bool IsDeepDataPossible { get; set; }

    public bool IsNullable { get; set; }
}
