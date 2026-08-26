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

/// <summary>
/// A span of consecutive rows sharing one origin, being an RLE run or a stretch of bit packed values
/// </summary>
public readonly record struct SegmentDataIdRun(SegmentValueOrigin Origin,
                                               long Value,
                                               int BitpackIndex,
                                               int FirstRow,
                                               int RowCount);
