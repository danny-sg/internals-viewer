using InternalsViewer.Query.Events.BatchMode.Enums;

namespace InternalsViewer.Query.Events.BatchMode;

public sealed record SegmentScanEvent : EngineEvent
{
    public bool IsScanStart { get; set; }

    public int NodeId { get; set; }

    public long RowGroupId { get; set; }

    public int ColumnId { get; set; }

    public ColumnStoreEncodingType EncodingType { get; set; }

    public ColumnStoreDataType CompressedDataType { get; set; }

    public int SqlDataType { get; set; }

    public int BitPacking { get; set; }

    public long BaseId { get; set; }

    public double Magnitude { get; set; }

    public long NullValue { get; set; }

    public long MinDataId { get; set; }

    public long MaxDataId { get; set; }

    public uint PrimaryDictionaryValueCount { get; set; }

    public uint SecondaryDictionaryValueCount { get; set; }

    public int SecondaryBaseId { get; set; }

    public ColumnStoreInstructionSet CpuInstructionSet { get; set; }

    public ColumnStoreFilterType FilterType { get; set; }

    public ColumnStoreEarlyFilterType FilterOnCompressedDataType { get; set; }

    public bool IsFilterOnCompressedDataUsed { get; set; }

    public bool IsDeepDataPossible { get; set; }

    public bool IsNullable { get; set; }

    public long InputRows { get; set; }

    public long OutputRows { get; set; }

    public long PureRowBuckets { get; set; }

    public long ImpureRowBuckets { get; set; }

    public override string Description => $"Segment Scan (Row Group {RowGroupId}, Column {ColumnId})";

    public bool HasScanResult => FoldedFrom is not null;

    public override string Detail
        => HasScanResult
            ? $"{EncodingType}, {CompressedDataType}, {BitPacking} bit, {FilterOnCompressedDataType} on compressed, "
              + $"{InputRows:N0} in, {OutputRows:N0} out"
            : $"{EncodingType}, {CompressedDataType}, {BitPacking} bit, {FilterOnCompressedDataType} on compressed";
}
