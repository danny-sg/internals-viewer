namespace InternalsViewer.Internals.Columnstore.Decoding;

public enum SegmentValueOrigin
{
    VariableLengthData,
    RleRun,
    BitPack
}

/// <summary>
/// A row Data Id with source Segment origin
/// </summary>
public readonly record struct SegmentDataIdSource(long DataId,
                                                  SegmentValueOrigin Origin,
                                                  int EntryIndex,
                                                  int SourceIndex);
