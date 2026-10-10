using InternalsViewer.Query.Events.Properties;
using InternalsViewer.Query.Events.BatchMode.Enums;

namespace InternalsViewer.Query.Events.BatchMode;

public sealed partial record SegmentScanEvent : EngineEvent
{
    [EventProperty("Scan Start")]
    public bool IsScanStart { get; set; }

    [EventProperty("Node")]
    public int NodeId { get; set; }

    [EventProperty("Row Group")]
    public long RowGroupId { get; set; }

    [EventProperty("Column")]
    public int ColumnId { get; set; }

    [EventProperty("Encoding")]
    public ColumnStoreEncodingType EncodingType { get; set; }

    [EventProperty("Compressed Data Type")]
    public ColumnStoreDataType CompressedDataType { get; set; }

    [EventProperty("SQL Data Type")]
    public int SqlDataType { get; set; }

    [EventProperty("Bit Packing")]
    public int BitPacking { get; set; }

    [EventProperty("Base Id")]
    public long BaseId { get; set; }

    [EventProperty("Magnitude")]
    public double Magnitude { get; set; }

    [EventProperty("Null Value")]
    public long NullValue { get; set; }

    [EventProperty("Min Data Id")]
    public long MinDataId { get; set; }

    [EventProperty("Max Data Id")]
    public long MaxDataId { get; set; }

    [EventProperty("Primary Dictionary Values", Type = EventPropertyType.Number)]
    public uint PrimaryDictionaryValueCount { get; set; }

    [EventProperty("Secondary Dictionary Values", Type = EventPropertyType.Number)]
    public uint SecondaryDictionaryValueCount { get; set; }

    [EventProperty("Secondary Base Id")]
    public int SecondaryBaseId { get; set; }

    [EventProperty("CPU Instruction Set")]
    public ColumnStoreInstructionSet? CpuInstructionSet { get; set; }

    [EventProperty("Filter Type")]
    public ColumnStoreFilterType FilterType { get; set; }

    [EventProperty("Compressed Data Filter")]
    public ColumnStoreEarlyFilterType FilterOnCompressedDataType { get; set; }

    [EventProperty("Filter On Compressed Data")]
    public bool IsFilterOnCompressedDataUsed { get; set; }

    [EventProperty("Deep Data Possible")]
    public bool IsDeepDataPossible { get; set; }

    [EventProperty("Nullable")]
    public bool IsNullable { get; set; }

    [EventProperty("Input Rows", Type = EventPropertyType.Number)]
    public long InputRows { get; set; }

    [EventProperty("Output Rows", Type = EventPropertyType.Number)]
    public long OutputRows { get; set; }

    [EventProperty("Pure Row Buckets", Type = EventPropertyType.Number)]
    public long PureRowBuckets { get; set; }

    [EventProperty("Impure Row Buckets", Type = EventPropertyType.Number)]
    public long ImpureRowBuckets { get; set; }

    public override string Name => "Segment Scan";

    public override string Description => $"Segment Scan (Row Group {RowGroupId}, Column {ColumnId})";

    public bool HasScanResult => FoldedFrom is not null;

    public override string Detail => $"Segment Scan: Row Group {RowGroupId}, Column {ColumnId}";
}
