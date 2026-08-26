namespace InternalsViewer.Query.Plans.Model;

public sealed class BatchInfo
{
    public long BatchCount { get; set; }

    public long SegmentReads { get; set; }

    public long SegmentSkips { get; set; }

    public long LocallyAggregatedRows { get; set; }

    public bool? IsFastComparisonUsed { get; set; }

    public bool? IsLocalAggregationUsed { get; set; }

    public bool? IsPrefiltered { get; set; }

    public bool? IsGlobalDictionaryUsed { get; set; }

    public string? GlobalDictionaryKeyColumns { get; set; }

    public List<SegmentScanInfo> SegmentScans { get; } = [];

    public string? CpuInstructionSet { get; set; }

    public bool? IsFilterOnCompressedDataUsed { get; set; }

    public bool? IsDeepDataPossible { get; set; }

    public long? PureRowBuckets { get; set; }

    public long? ImpureRowBuckets { get; set; }

}
